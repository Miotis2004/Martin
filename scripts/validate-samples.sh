#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SAMPLES_ROOT="$ROOT/samples"
CLI_PROJECT="$ROOT/Martin/src/Martin.Cli/Martin.Cli.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
WORK_ROOT="${MARTIN_SAMPLE_WORK_ROOT:-}"
KEEP_WORK="${MARTIN_SAMPLE_KEEP_WORK:-0}"

samples=(HelloMartin Functions StructsAndClasses Optionals EnumsAndPatterns Protocols Generics TypedErrors)

if [[ ! -f "$CLI_PROJECT" ]]; then
  echo "Martin CLI project not found: $CLI_PROJECT" >&2
  exit 1
fi

if [[ -z "$WORK_ROOT" ]]; then
  WORK_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/martin-samples.XXXXXX")"
else
  rm -rf "$WORK_ROOT"
  mkdir -p "$WORK_ROOT"
fi

cleanup() {
  if [[ "$KEEP_WORK" != "1" ]]; then
    rm -rf "$WORK_ROOT"
  else
    echo "Kept sample validation workspace: $WORK_ROOT"
  fi
}
trap cleanup EXIT

failures=0
for sample in "${samples[@]}"; do
  source_dir="$SAMPLES_ROOT/$sample"
  isolated_dir="$WORK_ROOT/$sample"
  cp -R "$source_dir" "$isolated_dir"
  rm -rf "$isolated_dir/bin" "$isolated_dir/obj" "$isolated_dir/.martin"

  echo "==> $sample: build"
  if ! dotnet run --project "$CLI_PROJECT" -- build "$isolated_dir" --configuration "$CONFIGURATION" --quiet; then
    echo "FAIL $sample: build failed" >&2
    failures=$((failures + 1))
    continue
  fi

  echo "==> $sample: run"
  actual="$WORK_ROOT/$sample.stdout"
  if ! dotnet run --project "$CLI_PROJECT" -- run "$isolated_dir" --configuration "$CONFIGURATION" --quiet > "$actual"; then
    echo "FAIL $sample: run failed" >&2
    failures=$((failures + 1))
    continue
  fi

  if ! cmp -s "$source_dir/expected.stdout" "$actual"; then
    echo "FAIL $sample: output differed" >&2
    diff -u "$source_dir/expected.stdout" "$actual" || true
    failures=$((failures + 1))
    continue
  fi

  echo "PASS $sample"
done

if [[ $failures -ne 0 ]]; then
  echo "$failures sample validation failure(s)." >&2
  exit 1
fi

echo "All samples validated from isolated copies."
