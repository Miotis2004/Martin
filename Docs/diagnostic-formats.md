# Diagnostic formats

Martin command-line tools support human diagnostics and, for build/compile paths, JSON diagnostics.

## Human diagnostics

Human diagnostics are written to stderr. A diagnostic contains a stable code, severity, message, and optional file location:

```text
Sources/main.martin(4,11): error MRT2004: Name 'missing' does not exist.
```

When source excerpts are available, renderers may include the relevant line and caret span. Parser and command-line errors use stable `MRT45xx` command codes. Project-system errors use `MRT40xx` and `MRT45xx` project codes. Compiler diagnostics generally use `MRT2xxx`; build diagnostics use `MRT3xxx`; run/freshness diagnostics use `MRT48xx`; unavailable test execution uses `MRT4901`.

## JSON diagnostics

JSON diagnostic mode is selected with:

```text
--diagnostic-format json
```

The JSON payload is a single envelope object with a `diagnostics` array. Object properties use camelCase and are emitted in a deterministic order by the current renderer. Nullable properties are omitted when their values are not present.

```json
{
  "diagnostics": [
    {
      "code": "MRT2004",
      "severity": "error",
      "message": "Name 'missing' does not exist.",
      "location": {
        "filePath": "Sources/main.martin",
        "startLine": 4,
        "startColumn": 11,
        "endLine": 4,
        "endColumn": 18
      },
      "relatedLocations": []
    }
  ]
}
```

Diagnostics that carry a non-source path include a diagnostic-level `path` property. Source spans are represented only by the nested `location` object. The JSON contract does not use a flat root array, flat location fields on diagnostics, or a `source` property.

JSON mode is intended for tooling. Do not parse human text when JSON diagnostics are available. JSON diagnostic output does not include banners, color, progress text, or source decoration.

## Stable diagnostic expectations

Every diagnostic code is intended to identify one class of failure. Scripts should primarily branch on process exit code, then use diagnostic codes for more detailed reporting.
