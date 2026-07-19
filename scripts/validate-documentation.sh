#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
spec_dir="$repo_root/Docs/specification"
required=(
  README.md
  lexical-grammar.md
  syntax-grammar.md
  types-and-values.md
  declarations.md
  expressions.md
  statements.md
  structs-and-classes.md
  optionals.md
  enums-and-patterns.md
  protocols.md
  generics.md
  typed-errors.md
  runtime-and-standard-library.md
  implementation-limits.md
)

for chapter in "${required[@]}"; do
  path="$spec_dir/$chapter"
  if [[ ! -f "$path" ]]; then
    echo "Missing required specification chapter: Docs/specification/$chapter" >&2
    exit 1
  fi
  if [[ "$chapter" != "README.md" ]] && ! grep -q '\*\*Language version:\*\* `0.1`' "$path"; then
    echo "Missing language-version metadata in Docs/specification/$chapter" >&2
    exit 1
  fi
  if ! grep -q '\*\*Owner:\*\*' "$path"; then
    echo "Missing owner metadata in Docs/specification/$chapter" >&2
    exit 1
  fi
  if ! grep -q '\*\*Specification status:\*\*' "$path"; then
    echo "Missing specification-status metadata in Docs/specification/$chapter" >&2
    exit 1
  fi
done

python3 - "$repo_root" <<'PY'
from pathlib import Path
import re
import sys

root = Path(sys.argv[1])
markdown_files = [p for p in (root / 'Docs').rglob('*.md')]
errors = []
link_pattern = re.compile(r'\[[^\]]+\]\(([^)]+)\)')
heading_pattern = re.compile(r'^(#{1,6})\s+(.+?)\s*#*$', re.MULTILINE)

def slugify(text: str) -> str:
    text = re.sub(r'`([^`]*)`', r'\1', text.strip()).lower()
    text = re.sub(r'[^a-z0-9\s-]', '', text)
    text = re.sub(r'\s+', '-', text.strip())
    return text

for path in markdown_files:
    text = path.read_text(encoding='utf-8')
    if path.is_relative_to(root / 'Docs/specification'):
        seen = set()
        for match in heading_pattern.finditer(text):
            anchor = slugify(match.group(2))
            if anchor in seen:
                errors.append(f'Duplicate anchor #{anchor} in {path.relative_to(root)}')
            seen.add(anchor)
    for match in link_pattern.finditer(text):
        target = match.group(1).strip()
        if target.startswith(('http://', 'https://', 'mailto:')):
            continue
        file_part, _, anchor = target.partition('#')
        if not file_part:
            target_path = path
        else:
            target_path = (path.parent / file_part).resolve()
            try:
                target_path.relative_to(root.resolve())
            except ValueError:
                continue
        if not target_path.exists():
            errors.append(f'Broken link in {path.relative_to(root)}: {target}')
            continue
        if anchor and target_path.suffix.lower() == '.md':
            target_text = target_path.read_text(encoding='utf-8')
            target_anchors = {slugify(m.group(2)) for m in heading_pattern.finditer(target_text)}
            if anchor not in target_anchors:
                errors.append(f'Broken anchor in {path.relative_to(root)}: {target}')

spec_readme = root / 'Docs/specification/README.md'
readme_text = spec_readme.read_text(encoding='utf-8')
required_links = [
    'lexical-grammar.md','syntax-grammar.md','types-and-values.md','declarations.md',
    'expressions.md','statements.md','structs-and-classes.md','optionals.md',
    'enums-and-patterns.md','protocols.md','generics.md','typed-errors.md',
    'runtime-and-standard-library.md','implementation-limits.md'
]
for link in required_links:
    if f']({link})' not in readme_text:
        errors.append(f'Missing specification navigation link: {link}')

if errors:
    for error in errors:
        print(error, file=sys.stderr)
    sys.exit(1)
print('Documentation validation passed.')
PY
