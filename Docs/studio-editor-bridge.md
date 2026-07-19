# Martin Studio Editor Bridge

Martin Studio hosts Monaco Editor through WebView2. Phase 11 requires local editor assets, typed message envelopes, request correlation, validation, diagnostics markers, and controlled WebView2 recovery.

## Asset policy

Editor assets are loaded from the application package/output, not from a CDN. Required asset groups are:

```text
Assets/Editor/index.html
Assets/Editor/editor.js
Assets/Editor/editor.css
Assets/Editor/martin-language.js
Assets/Editor/monaco/
Assets/Editor/themes/
Assets/Editor/licenses/
```

Startup validation should fail with an actionable Studio diagnostic when assets are missing. Packaged and unpackaged launches must both include these files.

## Hosting model

Studio uses one WebView2 instance, one Monaco runtime, and one Monaco editor. Each open document maps to one Monaco model. Tab selection activates models; closing a tab disposes that model.

Do not create one WebView2 per document tab.

## JSON envelope

Messages use a small typed envelope:

```json
{
  "type": "openDocument",
  "id": "optional-request-id",
  "payload": {}
}
```

Host-to-editor messages include initialization, open/close/activate document, replace text, set diagnostics, reveal range, set read-only, set theme, focus, request current text, restore view state, and execute editor command.

Editor-to-host messages include editor ready, document changed, cursor changed, selection changed, save requested, document text response, view-state changed, editor error, and request completed.

## Validation rules

Validate every message before applying it:

- JSON shape and `type` are required.
- Message size is bounded.
- Request IDs must match pending requests.
- Document IDs must refer to known documents.
- Text payloads must stay within configured limits.
- Ranges must be valid and normalized.
- Diagnostics must include severity and message.
- Theme names must be known.
- Commands that require readiness must wait until the bridge is ready.

Invalid messages should be ignored or rejected with `MRT5203` and logged without dumping source text.

## Security rules

- Load only local editor files.
- Block external navigation, popups, and downloads.
- Disable unnecessary browser UI.
- Disable DevTools in Release builds.
- Avoid host-object exposure unless a narrowly audited API is required.
- Never evaluate Martin source as JavaScript.

## Recovery

On WebView2 failure, Studio should mark the editor unavailable, preserve all in-memory document text, cancel pending requests, attempt one controlled restart, reload assets, recreate models, restore the active document and view state, reapply diagnostics, and report persistent failure with `MRT5206`.
## Logging and recovery

Studio writes structured JSON Lines logs through `IStudioLogService`. Use **Help > Open Logs Folder** to open the current log directory. Logged events include project creation/open/close and transition decisions, save/save-as results and external-change conflicts, build/clean/cancel transitions, run/stop/external-terminal transitions, editor bridge validation failures and recovery attempts, settings/session corruption recovery, watcher overflow, and unexpected shell failures.

Logs are intended for diagnostics and may include operation names, document IDs, paths, counts, statuses, diagnostics counts, exception types, and short technical details. They must not include complete editor text or complete source-bearing editor protocol payloads.
