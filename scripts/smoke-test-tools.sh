#!/usr/bin/env bash
set -euo pipefail

configuration="Release"
keep_artifacts=0
verbose_output=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration|-c)
      configuration="${2:?missing value for $1}"
      shift 2
      ;;
    --keep-artifacts)
      keep_artifacts=1
      shift
      ;;
    --verbose-output|-v)
      verbose_output=1
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$script_dir"
while [[ "$repo_root" != "/" && ! -f "$repo_root/Martin/Martin.slnx" ]]; do
  repo_root="$(dirname -- "$repo_root")"
done

if [[ ! -f "$repo_root/Martin/Martin.slnx" ]]; then
  echo "Unable to resolve repository root from '$script_dir'." >&2
  exit 1
fi

artifacts_root="$repo_root/artifacts/tool-smoke-tests"
run_root="$artifacts_root/$(date -u +%Y%m%d%H%M%S%3N)"
package_root="$run_root/packages"
tool_root="$run_root/tools"
workspace_root="$run_root/workspace"
project_root="$workspace_root/SmokeProject"
compiler_root="$workspace_root/compiler"
compiler_source="$compiler_root/main.martin"
compiler_output="$compiler_root/out"
package_listing="$run_root/package-contents.txt"

cleanup() {
  if [[ "$keep_artifacts" -eq 0 && -d "$run_root" ]]; then
    rm -rf -- "$run_root"
  fi
}
trap cleanup EXIT

run() {
  if [[ "$verbose_output" -eq 1 ]]; then
    printf '> '
    printf '%q ' "$@"
    printf '\n'
  fi
  "$@"
}

mkdir -p -- "$package_root" "$tool_root" "$workspace_root" "$compiler_root"

run dotnet restore "$repo_root/Martin/Martin.slnx"
run dotnet build "$repo_root/Martin/Martin.slnx" --configuration "$configuration" --no-restore -p:Platform=x64
run dotnet pack "$repo_root/Martin/src/Martin.Cli/Martin.Cli.csproj" --configuration "$configuration" --no-build --output "$package_root" -p:Platform=x64
run dotnet pack "$repo_root/Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj" --configuration "$configuration" --no-build --output "$package_root" -p:Platform=x64

: > "$package_listing"
python3 - "$package_listing" "$package_root"/*.nupkg <<'PY'
import re
import sys
import zipfile

listing_path = sys.argv[1]
packages = sys.argv[2:]
residue = re.compile(r'(^|/)(\.git|bin|obj|TestResults|artifacts|node_modules|\.vs|.*\.(user|suo|orig|rej|tmp|temp|bak|cache))(/|$)')
with open(listing_path, 'w', encoding='utf-8') as listing:
    for package in packages:
        with zipfile.ZipFile(package) as archive:
            for name in archive.namelist():
                listing.write(name + '\n')
                if residue.search(name):
                    raise SystemExit(f"Package contains development residue: {package} entry {name}")
PY

run dotnet tool install Martin.Tool --tool-path "$tool_root" --add-source "$package_root"
run dotnet tool install Martin.Compiler.Tool --tool-path "$tool_root" --add-source "$package_root"

martin="$tool_root/martin"
martinc="$tool_root/martinc"

(
  cd "$workspace_root"
  run "$martin" --version
  run "$martinc" --version
  run "$martin" new SmokeProject --path "$workspace_root" --no-git --force
)

(
  cd "$project_root"
  run "$martin" build
  run "$martin" run
  run "$martin" run --no-build
  run "$martin" clean --dry-run
  run "$martin" clean
)

printf '%s' 'func main() { print("hello from martinc smoke test") }' > "$compiler_source"
(
  cd "$compiler_root"
  run "$martinc" "$compiler_source" --output "$compiler_output" --name SmokeCompiler --quiet
)

run dotnet tool uninstall Martin.Tool --tool-path "$tool_root"
run dotnet tool uninstall Martin.Compiler.Tool --tool-path "$tool_root"

echo "Packaging smoke tests completed successfully."
echo "Artifacts: $run_root"
