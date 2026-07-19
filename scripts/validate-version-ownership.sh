#!/usr/bin/env bash
set -euo pipefail

product_version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Martin/Directory.Build.props | head -n1)
assembly_version=$(sed -n 's:.*<AssemblyVersion>\(.*\)</AssemblyVersion>.*:\1:p' Martin/Directory.Build.props | head -n1)
file_version=$(sed -n 's:.*<FileVersion>\(.*\)</FileVersion>.*:\1:p' Martin/Directory.Build.props | head -n1)
language_version=$(sed -n 's/.*public const string Current = "\(.*\)";.*/\1/p' Martin/src/Martin.Compiler/LanguageVersion.cs | head -n1)
manifest_version=$(sed -n 's/.*SupportedManifestVersion = \([0-9][0-9]*\);.*/\1/p' Martin/src/Martin.ProjectSystem/ManifestParser.cs | head -n1)
msix_version=$(sed -n 's/.*Version="\([^"]*\)".*/\1/p' Martin/src/Martin.Studio.WinUI/Package.appxmanifest | head -n1)
expected_msix=${product_version%%-*}.0

fail=0
check() {
  local name=$1 actual=$2 expected=$3
  if [[ "$actual" != "$expected" ]]; then
    printf 'Version mismatch: %s expected %s but found %s\n' "$name" "$expected" "$actual" >&2
    fail=1
  fi
}

check product "$product_version" "0.1.0-alpha"
check assembly "$assembly_version" "0.1.0.0"
check file "$file_version" "0.1.0.0"
check language "$language_version" "0.1"
check manifest "$manifest_version" "1"
check msix "$msix_version" "$expected_msix"

if ! rg -q 'Language version: \{versions\.LanguageVersion\}' Martin/src/Martin.Cli/CommandHandlers.cs; then
  printf 'Version mismatch: martin version output does not include language version.\n' >&2
  fail=1
fi
if ! rg -q 'Language version: \{Versions\.LanguageVersion\}' Martin/src/Martin.Compiler.Cli/Program.cs; then
  printf 'Version mismatch: martinc version output does not include language version.\n' >&2
  fail=1
fi
if ! rg -q '`0\.1\.0-alpha`' Docs/compatibility.md || ! rg -q '`0\.1`' Docs/compatibility.md; then
  printf 'Version mismatch: Docs/compatibility.md does not document product and language versions.\n' >&2
  fail=1
fi

if [[ $fail -ne 0 ]]; then
  exit 1
fi

printf 'Version ownership validation passed: product=%s language=%s manifest=%s msix=%s\n' "$product_version" "$language_version" "$manifest_version" "$msix_version"
