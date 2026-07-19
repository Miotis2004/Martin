#!/usr/bin/env python3
"""Validate the approved Martin alpha candidate and write a publication plan."""
from __future__ import annotations

import argparse, datetime as dt, hashlib, json, pathlib, subprocess, sys


def run(args, cwd, check=True):
    proc = subprocess.run(args, cwd=cwd, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and proc.returncode != 0:
        raise RuntimeError(proc.stderr.strip() or proc.stdout.strip() or 'command failed')
    return proc.stdout.strip(), proc.returncode


def sha256(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding='utf-8'))


def main() -> int:
    p = argparse.ArgumentParser(description='Prepare the Martin alpha publication checklist without publishing external state.')
    p.add_argument('--version', default='0.1.0-alpha')
    p.add_argument('--review', required=True, help='Approved Milestone 17 review.json')
    p.add_argument('--output', help='Defaults to artifacts/publication/<version>/<commit>/publication-plan.json')
    ns = p.parse_args()

    repo = pathlib.Path(run(['git','rev-parse','--show-toplevel'], pathlib.Path.cwd())[0])
    head = run(['git','rev-parse','--verify','HEAD'], repo)[0]
    review_path = (repo / ns.review).resolve()
    review = load_json(review_path)
    commit = review.get('commit')
    tag = f'v{ns.version}'
    release_root = repo / 'artifacts' / 'release' / ns.version
    manifest_path = release_root / 'validation' / 'manifest.json'
    checksum_path = release_root / 'checksums' / 'SHA256SUMS'

    checks = []
    blockers = []
    def add(name, ok, detail=''):
        checks.append({'name': name, 'status': 'passed' if ok else 'failed', 'detail': detail})
        if not ok:
            blockers.append(f'{name}: {detail}')

    dirty = subprocess.run(['git','diff','--quiet','--','.',':!artifacts'], cwd=repo).returncode != 0
    staged = subprocess.run(['git','diff','--cached','--quiet','--','.',':!artifacts'], cwd=repo).returncode != 0
    add('approved review passed', review.get('status') == 'passed' and review.get('approval') == 'approved', f"status={review.get('status')} approval={review.get('approval')}")
    add('review version matches', review.get('version') == ns.version, str(review.get('version')))
    add('review tag matches', review.get('tag') == tag, str(review.get('tag')))
    add('candidate commit is HEAD', commit == head, f'candidate={commit} head={head}')
    add('tracked worktree is clean', not dirty and not staged, 'tracked changes outside artifacts are present' if dirty or staged else '')
    add('manifest exists', manifest_path.is_file(), str(manifest_path.relative_to(repo)))
    add('checksums exist', checksum_path.is_file(), str(checksum_path.relative_to(repo)))

    manifest = load_json(manifest_path) if manifest_path.is_file() else {}
    add('manifest matches approved candidate', manifest.get('commit') == commit and manifest.get('tag') == tag and manifest.get('productVersion') == ns.version, json.dumps({k: manifest.get(k) for k in ['commit','tag','productVersion']}))

    if checksum_path.is_file():
        bad = []
        for line in checksum_path.read_text(encoding='utf-8').splitlines():
            if not line.strip():
                continue
            expected, rel = line.split(None, 1)
            path = release_root / rel.strip()
            if not path.is_file() or sha256(path) != expected:
                bad.append(rel.strip())
        add('artifact hashes still match', not bad, ', '.join(bad[:5]))

    existing_tag, rc = run(['git','rev-parse','--verify',tag], repo, check=False)
    add('local tag absent or matches candidate', rc != 0 or existing_tag == commit, existing_tag if rc == 0 else 'tag does not exist locally')

    packages = sorted(str(p.relative_to(repo)).replace('\\','/') for p in (release_root / 'packages').glob('*.nupkg')) if (release_root / 'packages').is_dir() else []
    plan = {
        'status': 'passed' if not blockers else 'failed',
        'version': ns.version,
        'tag': tag,
        'commit': commit,
        'dateUtc': dt.datetime.utcnow().replace(microsecond=0).isoformat() + 'Z',
        'review': str(review_path.relative_to(repo)),
        'manifest': str(manifest_path.relative_to(repo)),
        'checksums': str(checksum_path.relative_to(repo)),
        'checks': checks,
        'blockers': blockers,
        'manualPublicationCommands': [
            f'git tag -a {tag} {commit} -m "Martin {ns.version}"',
            f'git push origin {tag}',
            'Create GitHub prerelease and upload staged artifacts plus checksums.',
            *[f'dotnet nuget push {pkg} --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY>' for pkg in packages],
            'Verify public downloads and fresh NuGet tool installation.'
        ]
    }
    out = repo / (ns.output or f'artifacts/publication/{ns.version}/{commit}/publication-plan.json')
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(plan, indent=2) + '\n', encoding='utf-8')
    print(f'Publication plan written to {out.relative_to(repo)}')
    if blockers:
        print('Blockers:')
        for b in blockers:
            print(f'- {b}')
    return 0 if not blockers else 1


if __name__ == '__main__':
    sys.exit(main())
