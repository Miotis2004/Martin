#!/usr/bin/env bash
set -euo pipefail

version="0.1.0-alpha"
configuration="Release"
platform="x64"
include_studio=0
skip_clean=0
verbose=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version|-v) version="${2:?missing value for $1}"; shift 2 ;;
    --configuration|-c) configuration="${2:?missing value for $1}"; shift 2 ;;
    --platform|-p) platform="${2:?missing value for $1}"; shift 2 ;;
    --include-studio) include_studio=1; shift ;;
    --skip-clean) skip_clean=1; shift ;;
    --verbose) verbose=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$script_dir"
while [[ "$repo_root" != "/" && ! -f "$repo_root/Martin/Martin.slnx" ]]; do repo_root="$(dirname -- "$repo_root")"; done
[[ -f "$repo_root/Martin/Martin.slnx" ]] || { echo "Unable to resolve repository root." >&2; exit 1; }
cd "$repo_root"

run_id="$(date -u +%Y%m%d%H%M%S)"
evidence_root="$repo_root/artifacts/release-validation/$version/$run_id"
logs_dir="$evidence_root/logs"
mkdir -p "$logs_dir"
outcomes="$evidence_root/outcomes.jsonl"
summary="$evidence_root/summary.json"

json_escape() { python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"; }
record() {
  local gate="$1" status="$2" log="$3" message="${4:-}"
  printf '{"gate":%s,"status":%s,"log":%s,"message":%s}\n' "$(json_escape "$gate")" "$(json_escape "$status")" "$(json_escape "${log#$repo_root/}")" "$(json_escape "$message")" >> "$outcomes"
}
run_gate() {
  local gate="$1"; shift
  local log="$logs_dir/${gate//[^A-Za-z0-9_.-]/_}.log"
  echo "==> $gate"
  if [[ "$verbose" -eq 1 ]]; then printf '> %q ' "$@"; printf '\n'; fi
  if "$@" >"$log" 2>&1; then
    record "$gate" "passed" "$log"
  else
    local code=$?
    record "$gate" "failed" "$log" "exit code $code"
    python3 - "$outcomes" "$summary" "$version" "$configuration" "$platform" "$repo_root" "$evidence_root" "failed" "$gate" <<'PY'
import json, subprocess, sys, datetime, pathlib
outcomes, summary, version, config, platform, root, evidence, status, failed = sys.argv[1:10]
commit=subprocess.check_output(['git','rev-parse','HEAD'], cwd=root, text=True).strip()
repo=subprocess.run(['git','config','--get','remote.origin.url'], cwd=root, text=True, stdout=subprocess.PIPE).stdout.strip() or 'unknown'
items=[json.loads(line) for line in pathlib.Path(outcomes).read_text(encoding='utf-8').splitlines() if line]
pathlib.Path(summary).write_text(json.dumps({'status':status,'failedGate':failed,'version':version,'configuration':config,'platform':platform,'dateUtc':datetime.datetime.utcnow().replace(microsecond=0).isoformat()+'Z','commit':commit,'repository':repo,'evidenceRoot':str(pathlib.Path(evidence).relative_to(root)),'outcomes':items}, indent=2)+'\n', encoding='utf-8')
PY
    echo "Release validation failed at gate: $gate" >&2
    echo "Log: $log" >&2
    exit "$code"
  fi
}

run_gate "repository identity and commit SHA" bash -c 'git rev-parse --show-toplevel && git rev-parse --verify HEAD && (git config --get remote.origin.url || true)'
if [[ "$skip_clean" -eq 0 ]]; then run_gate "clean tracked worktree" bash -c 'git diff --quiet -- . ":!artifacts" && git diff --cached --quiet -- . ":!artifacts"'; else record "clean tracked worktree" "skipped" "$logs_dir/clean_tracked_worktree.log" "--skip-clean"; fi
run_gate "version consistency" bash scripts/validate-version-ownership.sh
run_gate "required documentation and public files" bash -c 'test -f README.md && test -f CHANGELOG.md && test -f LICENSE && test -f SECURITY.md && test -f CONTRIBUTING.md && test -f Docs/releases/'"$version"'.md && test -f Docs/compatibility.md && test -f Docs/known-limitations.md'
run_gate "diagnostic catalog coverage" bash scripts/validate-diagnostics.sh
run_gate "specification structure and links" bash scripts/validate-documentation.sh
run_gate "restore" dotnet restore Martin/Martin.slnx
run_gate "debug build" dotnet build Martin/Martin.slnx --configuration Debug --no-restore -p:Platform="$platform"
run_gate "release build" dotnet build Martin/Martin.slnx --configuration "$configuration" --no-restore -p:Platform="$platform"
run_gate "local test suite" dotnet test Martin/Martin.slnx --configuration "$configuration" --no-build -p:Platform="$platform" --logger "trx;LogFileName=release-validation.trx" --results-directory "$evidence_root/test-results"
run_gate "sample build and execution" bash scripts/validate-samples.sh
run_gate "formatter check" bash -c 'for f in samples/*/Sources/*.martin; do dotnet run --project Martin/src/Martin.Cli/Martin.Cli.csproj -- format "$f" --check; done'
run_gate "tool packing" dotnet pack Martin/src/Martin.Cli/Martin.Cli.csproj --configuration "$configuration" --no-build --output "$evidence_root/tool-packages" -p:Platform="$platform"
run_gate "runtime packing" dotnet pack Martin/src/Martin.Runtime/Martin.Runtime.csproj --configuration "$configuration" --no-build --output "$evidence_root/runtime-packages" -p:Platform="$platform"
run_gate "package-content inspection and release manifest" bash scripts/package-alpha.sh --version "$version" --configuration "$configuration" --platform "$platform" --skip-build
run_gate "isolated tool installation and CLI/compiler smoke workflows" bash scripts/smoke-test-tools.sh --configuration "$configuration" --keep-artifacts

if [[ "$include_studio" -eq 1 ]]; then
  if [[ "$(uname -s)" == MINGW* || "$(uname -s)" == MSYS* || "$(uname -s)" == CYGWIN* ]]; then
    run_gate "Studio publish on Windows" dotnet publish Martin/src/Martin.Studio.WinUI/Martin.Studio.WinUI.csproj --configuration "$configuration" -p:Platform="$platform" -p:RuntimeIdentifier="win-$platform"
    run_gate "Studio manual-smoke record" bash -c 'test -f Docs/studio-packaging.md && printf "Manual Studio smoke must be recorded in release review.\n"'
  else
    record "Studio publish on Windows" "incomplete" "$logs_dir/Studio_publish_on_Windows.log" "requires Windows $platform"
    record "Studio manual-smoke record" "incomplete" "$logs_dir/Studio_manual-smoke_record.log" "requires Windows manual smoke"
  fi
else
  record "Studio publish on Windows" "skipped" "$logs_dir/Studio_publish_on_Windows.log" "pass --include-studio on Windows for complete alpha validation"
  record "Studio manual-smoke record" "skipped" "$logs_dir/Studio_manual-smoke_record.log" "pass --include-studio on Windows for complete alpha validation"
fi
run_gate "artifact checksums and release manifest" bash -c 'test -s artifacts/release/'"$version"'/checksums/SHA256SUMS && python3 -m json.tool artifacts/release/'"$version"'/validation/manifest.json >/dev/null'

python3 - "$outcomes" "$summary" "$version" "$configuration" "$platform" "$repo_root" "$evidence_root" <<'PY'
import json, subprocess, sys, datetime, pathlib
outcomes, summary, version, config, platform, root, evidence = sys.argv[1:8]
commit=subprocess.check_output(['git','rev-parse','HEAD'], cwd=root, text=True).strip()
repo=subprocess.run(['git','config','--get','remote.origin.url'], cwd=root, text=True, stdout=subprocess.PIPE).stdout.strip() or 'unknown'
items=[json.loads(line) for line in pathlib.Path(outcomes).read_text(encoding='utf-8').splitlines() if line]
status='incomplete' if any(i['status']=='incomplete' for i in items) else 'passed'
pathlib.Path(summary).write_text(json.dumps({'status':status,'version':version,'configuration':config,'platform':platform,'dateUtc':datetime.datetime.utcnow().replace(microsecond=0).isoformat()+'Z','commit':commit,'repository':repo,'evidenceRoot':str(pathlib.Path(evidence).relative_to(root)),'outcomes':items}, indent=2)+'\n', encoding='utf-8')
PY

echo "Release validation completed. Summary: $summary"
