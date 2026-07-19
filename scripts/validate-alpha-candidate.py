#!/usr/bin/env python3
"""Validate and record the Martin alpha release-candidate review."""
from __future__ import annotations

import argparse, datetime as dt, hashlib, json, os, pathlib, subprocess, sys


def run(args, cwd):
    return subprocess.check_output(args, cwd=cwd, text=True).strip()


def sha256(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding='utf-8'))


def main() -> int:
    p = argparse.ArgumentParser(description='Validate one immutable Martin alpha release candidate and write review evidence.')
    p.add_argument('--version', default='0.1.0-alpha')
    p.add_argument('--manifest', help='Release manifest path; defaults to artifacts/release/<version>/validation/manifest.json')
    p.add_argument('--checksums', help='Checksum file path; defaults to artifacts/release/<version>/checksums/SHA256SUMS')
    p.add_argument('--validation-summary', help='Local release validation summary.json from scripts/validate-release.*')
    p.add_argument('--studio-summary', help='Windows Studio validation summary.json, when recorded separately')
    p.add_argument('--require-studio', action='store_true', help='Fail unless Studio validation is passed in supplied evidence')
    p.add_argument('--approval', choices=['pending','approved','blocked'], default='pending')
    p.add_argument('--blocker', action='append', default=[], help='Known release blocker. Repeat for multiple blockers.')
    p.add_argument('--output', help='Review output path; defaults to artifacts/release-candidate/<version>/<commit>/review.json')
    ns = p.parse_args()

    repo = pathlib.Path(run(['git','rev-parse','--show-toplevel'], pathlib.Path.cwd()))
    commit = run(['git','rev-parse','--verify','HEAD'], repo)
    status = subprocess.run(['git','diff','--quiet','--','.',':!artifacts'], cwd=repo)
    cached = subprocess.run(['git','diff','--cached','--quiet','--','.',':!artifacts'], cwd=repo)
    clean = status.returncode == 0 and cached.returncode == 0

    manifest_path = repo / (ns.manifest or f'artifacts/release/{ns.version}/validation/manifest.json')
    checksum_path = repo / (ns.checksums or f'artifacts/release/{ns.version}/checksums/SHA256SUMS')
    checks = []
    blockers = list(ns.blocker)

    def add(name, ok, detail=''):
        checks.append({'name': name, 'status': 'passed' if ok else 'failed', 'detail': detail})
        if not ok: blockers.append(f'{name}: {detail}')

    add('candidate commit is full SHA', len(commit) == 40, commit)
    add('tracked worktree is clean', clean, 'tracked changes outside artifacts are present' if not clean else '')
    add('release manifest exists', manifest_path.is_file(), str(manifest_path.relative_to(repo) if manifest_path.exists() else manifest_path))
    add('checksum file exists', checksum_path.is_file(), str(checksum_path.relative_to(repo) if checksum_path.exists() else checksum_path))

    manifest = None
    if manifest_path.is_file():
        manifest = load_json(manifest_path)
        add('manifest version matches', manifest.get('productVersion') == ns.version, str(manifest.get('productVersion')))
        add('manifest commit matches candidate', manifest.get('commit') == commit, str(manifest.get('commit')))
        add('manifest tag plan matches', manifest.get('tag') == f'v{ns.version}', str(manifest.get('tag')))
    if checksum_path.is_file():
        bad = []
        for i, line in enumerate(checksum_path.read_text(encoding='utf-8').splitlines(), 1):
            if not line.strip(): continue
            parts = line.split(None, 1)
            if len(parts) != 2:
                bad.append(f'line {i} is malformed'); continue
            expected, rel = parts[0], parts[1].strip()
            f = checksum_path.parents[1] / rel
            if not f.is_file(): bad.append(f'missing {rel}')
            elif sha256(f) != expected: bad.append(f'hash mismatch {rel}')
        add('artifact hashes match checksum file', not bad, '; '.join(bad[:5]))
    if manifest and checksum_path.is_file():
        checksummed = {line.split(None,1)[1].strip() for line in checksum_path.read_text(encoding='utf-8').splitlines() if line.strip() and len(line.split(None,1))==2}
        manifest_files = {a['filename'] for a in manifest.get('artifacts', []) if a.get('filename') != 'checksums/SHA256SUMS'}
        missing = sorted(manifest_files - checksummed)
        add('manifest artifacts are checksummed', not missing, ', '.join(missing[:5]))

    summaries = []
    for label, given in [('release validation', ns.validation_summary), ('Studio validation', ns.studio_summary)]:
        if not given: continue
        path = repo / given
        data = load_json(path)
        summaries.append({'label':label, 'path':given, 'status':data.get('status'), 'commit':data.get('commit')})
        add(f'{label} commit matches candidate', data.get('commit') == commit, str(data.get('commit')))
        add(f'{label} passed', data.get('status') == 'passed', str(data.get('status')))
    if ns.require_studio:
        has_studio = any(s['label'] == 'Studio validation' and s['status'] == 'passed' for s in summaries)
        add('Windows Studio validation is present', has_studio, 'supply --studio-summary from a Windows complete validation run')
    if ns.approval == 'approved':
        add('approval has no blockers', not blockers, 'approval cannot be recorded while blockers exist')
    if ns.approval == 'blocked':
        add('blocked approval lists blockers', bool(ns.blocker), 'provide at least one --blocker')

    overall = 'passed' if all(c['status'] == 'passed' for c in checks) else 'failed'
    out = repo / (ns.output or f'artifacts/release-candidate/{ns.version}/{commit}/review.json')
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({'status': overall, 'approval': ns.approval, 'version': ns.version, 'tag': f'v{ns.version}', 'commit': commit, 'dateUtc': dt.datetime.utcnow().replace(microsecond=0).isoformat()+'Z', 'manifest': str(manifest_path.relative_to(repo)), 'checksums': str(checksum_path.relative_to(repo)), 'validationEvidence': summaries, 'checks': checks, 'blockers': sorted(set(blockers))}, indent=2) + '\n', encoding='utf-8')
    print(f'Release-candidate review written to {out.relative_to(repo)}')
    if blockers:
        print('Blockers:'); [print(f'- {b}') for b in sorted(set(blockers))]
    return 0 if overall == 'passed' else 1

if __name__ == '__main__':
    sys.exit(main())
