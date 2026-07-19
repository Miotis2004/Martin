# Martin Studio Troubleshooting

This page lists common Martin Studio failures and the expected recovery behavior.

## Project will not open

Check that the folder contains `Martin.toml` or select the manifest directly. Invalid projects should report project-system diagnostics and preserve the previous workspace.

## Editor does not load

Missing or corrupt editor assets should report `MRT5201`. WebView2 initialization failure should report `MRT5202`. Confirm packaged or unpackaged output contains `Assets/Editor` and the WebView2 runtime is installed.

## Editor stopped responding

WebView2 process failure should report `MRT5205`, preserve unsaved text, cancel pending requests, and attempt a controlled editor restart. Persistent recovery failure should report `MRT5206`.

## Save failed

Save failures should report `MRT5005`, keep the document dirty, preserve editor text, and leave the original file intact. Check file permissions, read-only attributes, external file locks, and disk space.

## External change detected

Clean files changed externally can be reloaded or kept. Dirty files changed externally require a deliberate choice: keep editor version, reload disk version, save as, or cancel. Studio must not silently overwrite external changes.

## Build failed or cancelled

Build failures appear in Output and Error List. Cancellation should not be treated as an internal error and should leave prior output available when possible.

## Run Without Build disabled

Run Without Build requires no dirty project source documents and a fresh published build state. Save changes and run Build first.

## Logs

Use Help > Open Logs Folder. Logs may contain operation names, paths, message types, request IDs, build stages, exit codes, exceptions, timing, cancellation, and recovery attempts. Logs should not contain full source text by default.

## Known limitations

Martin Studio is an alpha IDE. Multi-project solutions, debugger support, integrated terminal emulation, source control integration, plugins, and Phase 12 semantic language intelligence are not part of Phase 11.
## Logging and recovery

Studio writes structured JSON Lines logs through `IStudioLogService`. Use **Help > Open Logs Folder** to open the current log directory. Logged events include project creation/open/close and transition decisions, save/save-as results and external-change conflicts, build/clean/cancel transitions, run/stop/external-terminal transitions, editor bridge validation failures and recovery attempts, settings/session corruption recovery, watcher overflow, and unexpected shell failures.

Logs are intended for diagnostics and may include operation names, document IDs, paths, counts, statuses, diagnostics counts, exception types, and short technical details. They must not include complete editor text or complete source-bearing editor protocol payloads.
