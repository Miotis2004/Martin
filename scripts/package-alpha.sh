#!/usr/bin/env bash
set -euo pipefail

version="0.1.0-alpha"
configuration="Release"
platform="x64"
skip_build=0
verbose=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version|-v) version="${2:?missing value for $1}"; shift 2 ;;
    --configuration|-c) configuration="${2:?missing value for $1}"; shift 2 ;;
    --platform|-p) platform="${2:?missing value for $1}"; shift 2 ;;
    --skip-build) skip_build=1; shift ;;
    --verbose) verbose=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$script_dir"
while [[ "$repo_root" != "/" && ! -f "$repo_root/Martin/Martin.slnx" ]]; do repo_root="$(dirname -- "$repo_root")"; done
[[ -f "$repo_root/Martin/Martin.slnx" ]] || { echo "Unable to resolve repository root." >&2; exit 1; }
cd "$repo_root"

run() { [[ "$verbose" -eq 0 ]] || { printf '> '; printf '%q ' "$@"; printf '\n'; }; "$@"; }
json_escape() { python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"; }

if ! git diff --quiet -- . ':!artifacts'; then
  echo "Tracked source changes are present. Commit or stash changes before staging a release." >&2
  exit 1
fi
commit="$(git rev-parse HEAD)"
tag="v$version"
sdk="$(python3 - <<'PY'
import json
print(json.load(open('global.json', encoding='utf-8'))['sdk']['version'])
PY
)"
manifest_schema="$(rg -n "SupportedManifestVersion\s*=\s*([0-9]+)" Martin/src -o -r '$1' | head -n1 | sed 's/.*://')"
manifest_schema="${manifest_schema:-1}"
release_root="$repo_root/artifacts/release/$version"
packages_dir="$release_root/packages"
studio_dir="$release_root/studio"
docs_dir="$release_root/docs"
checksums_dir="$release_root/checksums"
validation_dir="$release_root/validation"
inspection_dir="$validation_dir/inspection"
rm -rf -- "$release_root"
mkdir -p -- "$packages_dir" "$studio_dir" "$docs_dir" "$checksums_dir" "$inspection_dir"

if [[ "$skip_build" -eq 0 ]]; then
  run dotnet restore Martin/Martin.slnx
  run dotnet build Martin/Martin.slnx --configuration "$configuration" --no-restore -p:Platform="$platform"
fi

pack_cmds=(
  "dotnet pack Martin/src/Martin.Cli/Martin.Cli.csproj --configuration $configuration --no-build --output $packages_dir -p:Platform=$platform"
  "dotnet pack Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj --configuration $configuration --no-build --output $packages_dir -p:Platform=$platform"
  "dotnet pack Martin/src/Martin.Runtime/Martin.Runtime.csproj --configuration $configuration --no-build --output $packages_dir -p:Platform=$platform"
)
for cmd in "${pack_cmds[@]}"; do run bash -c "$cmd"; done

cp Docs/releases/$version.md "$release_root/release-notes.md"
cp README.md CHANGELOG.md LICENSE SECURITY.md CONTRIBUTING.md "$docs_dir/"
cp -R Docs/specification "$docs_dir/specification"
cp Docs/diagnostics.md Docs/compatibility.md Docs/known-limitations.md Docs/tool-installation.md Docs/runtime-package-and-library-policy.md Docs/studio-packaging.md "$docs_dir/"

studio_publish="Martin/src/Martin.Studio.WinUI/bin/$platform/$configuration/net8.0-windows10.0.19041.0/win-$platform/AppPackages"
if [[ -d "$studio_publish" ]]; then
  find "$studio_publish" -type f \( -name '*.msix' -o -name '*.msixbundle' -o -name '*.appinstaller' -o -name '*.cer' \) -exec cp {} "$studio_dir/" \;
fi
cat > "$studio_dir/README.txt" <<STUDIO
Martin Studio artifacts are staged here when a Windows MSIX publish has already produced packages.
Run the Windows release validation path to create and inspect the signed or sideloadable Studio package.
STUDIO

python3 - "$packages_dir" "$inspection_dir" <<'PY'
import json, re, sys, zipfile
from pathlib import Path
packages = sorted(Path(sys.argv[1]).glob('*.nupkg'))
out = Path(sys.argv[2]); residue = re.compile(r'(^|/)(\.git|bin|obj|TestResults|artifacts|node_modules|\.vs|.*\.(user|suo|orig|rej|tmp|temp|bak|cache))(/|$)')
for package in packages:
    entries=[]
    with zipfile.ZipFile(package) as z:
        for info in sorted(z.infolist(), key=lambda i: i.filename):
            if residue.search(info.filename): raise SystemExit(f"Package contains development residue: {package} entry {info.filename}")
            entries.append({'name': info.filename, 'size': info.file_size, 'compressedSize': info.compress_size})
    (out / f'{package.name}.contents.json').write_text(json.dumps(entries, indent=2) + '\n', encoding='utf-8')
PY

sha_file="$checksums_dir/SHA256SUMS"
: > "$sha_file"
while IFS= read -r -d '' file; do
  rel="${file#$release_root/}"
  [[ "$rel" == "checksums/SHA256SUMS" ]] && continue
  sha256sum "$file" | awk -v r="$rel" '{print $1 "  " r}' >> "$sha_file"
done < <(find "$release_root" -type f -print0 | sort -z)

python3 - "$release_root" "$version" "$tag" "$commit" "$sdk" "$configuration" "$platform" "$manifest_schema" <<'PY'
import hashlib, json, os, sys
from pathlib import Path
root=Path(sys.argv[1]); version,tag,commit,sdk,config,platform,schema=sys.argv[2:9]
commands={'.nupkg':'dotnet pack','release-notes.md':'cp Docs/releases','docs':'document staging','studio':'Windows Studio publish staging'}
arts=[]
for p in sorted(root.rglob('*')):
    if not p.is_file() or p.relative_to(root).as_posix()=='validation/manifest.json': continue
    rel=p.relative_to(root).as_posix(); kind=rel.split('/')[0]
    h=hashlib.sha256(p.read_bytes()).hexdigest()
    cmd='sha256sum' if rel=='checksums/SHA256SUMS' else ('dotnet pack' if p.suffix=='.nupkg' else ('Studio publish staging' if kind=='studio' else 'release staging'))
    arts.append({'filename':rel,'kind':kind,'size':p.stat().st_size,'sha256':h,'producingCommand':cmd})
manifest={'productVersion':version,'tag':tag,'commit':commit,'repositorySdk':sdk,'generatedFramework':'net8.0','manifestSchema':int(schema),'configuration':config,'platform':platform,'releaseCommand':'scripts/package-alpha.sh','artifacts':arts}
(root/'validation'/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
PY

echo "Release candidate staged at $release_root"
echo "Manifest: $validation_dir/manifest.json"
echo "Checksums: $sha_file"
