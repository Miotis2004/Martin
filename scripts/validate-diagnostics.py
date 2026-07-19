#!/usr/bin/env python3
import json, pathlib, re, sys
root=pathlib.Path(__file__).resolve().parents[1]
src=root/'Martin'/'src'
catalog=root/'Docs'/'diagnostics'/'catalog.json'
source_codes={}
for path in src.rglob('*.cs'):
    text=path.read_text(encoding='utf-8', errors='ignore')
    for match in re.finditer(r'MRT\d{4}', text):
        source_codes.setdefault(match.group(0), set()).add(str(path.relative_to(root)))
data=json.loads(catalog.read_text(encoding='utf-8'))
entries=data.get('entries', [])
seen={}
errors=[]
required={'code','severity','title','message','explanation','cause','invalidExample','correctedExample','introducedInPhase','subsystem','status','documentation'}
for i,e in enumerate(entries):
    code=e.get('code')
    if not code or not re.fullmatch(r'MRT\d{4}', code): errors.append(f'entry {i}: invalid code {code!r}'); continue
    if code in seen: errors.append(f'duplicate catalog code {code}')
    seen[code]=e
    missing=sorted(k for k in required if not e.get(k))
    if missing: errors.append(f'{code}: missing {", ".join(missing)}')
    if e.get('severity') not in {'error','warning','info'}: errors.append(f'{code}: invalid severity {e.get("severity")!r}')
    if e.get('status') not in {'active','reserved','retired'}: errors.append(f'{code}: invalid status {e.get("status")!r}')
missing=sorted(set(source_codes)-set(seen))
extra=sorted(c for c,e in seen.items() if e.get('status')=='active' and c not in source_codes)
if missing: errors.append('missing source codes: '+', '.join(missing))
if extra: errors.append('active catalog codes not found in source: '+', '.join(extra))
if errors:
    print('Diagnostic catalog validation failed:', file=sys.stderr)
    for e in errors: print(' - '+e, file=sys.stderr)
    sys.exit(1)
print(f'Diagnostic catalog validation passed: {len(source_codes)} source codes and {len(entries)} catalog entries.')
