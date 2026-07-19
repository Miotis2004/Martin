# Martin Studio Project and Document Workflow

Martin Studio opens one Martin project at a time. A project can be selected by folder or by `Martin.toml`; both paths are resolved through `Martin.ProjectSystem`.

## Opening a project

Project opening should:

1. Resolve unsaved changes in the current project.
2. Stop active execution or cancel the transition.
3. Cancel active builds or cancel the transition.
4. Save current session state.
5. Dispose file watchers for the old project.
6. Load and validate the new project.
7. Build the project tree.
8. Restore session state for open documents, active tab, panes, and view state.

Invalid projects should preserve the previous workspace and show actionable diagnostics.

## Recent projects

Recent projects use canonical manifest paths, remove duplicates, sort newest first, remain bounded by settings, and tolerate missing paths. Reopen-last-project should use the most recent valid entry when enabled.

## Project Explorer

Explorer nodes include project, manifest, folders, Martin source files, other files, missing files, and generated folders. Generated output is hidden by default. Sorting is deterministic, folders appear before files, and traversal must stay within the project root.

Supported actions are open, reveal in File Explorer, copy path, and refresh. File-system changes are debounced and watcher overflow falls back to a full refresh.

## Document lifecycle

Opening a document canonicalizes the path, activates an already-open file instead of duplicating it, reads asynchronously, detects encoding and line endings, captures timestamp/hash identity, creates a document model, creates/activates the Monaco model, and restores view state.

An open document tracks:

- Canonical path
- Text
- Encoding
- Line endings
- Dirty state
- Read-only state
- Disk existence
- Timestamp and saved hash
- Document version
- Editor view state

## Dirty state and save

The editor text is authoritative. Dirty state changes when accepted editor text differs from the saved state. Build, run, diagnostics, save, and session view state must not silently use stale disk text for open documents.

Saving requests current editor text, checks external changes, preserves encoding and line endings, writes a temporary file in the destination directory, flushes it, atomically replaces or moves it into place, updates timestamp/hash, suppresses the corresponding watcher event, and clears dirty state only after success.

Save All uses deterministic order, continues after independent failures where safe, returns per-document results, and keeps failed documents dirty.

## Close behavior

Dirty close prompts use Save, Don't Save, and Cancel. Close project and application exit apply the same policy to all dirty documents.

Deleted, read-only, and externally changed files must not cause in-memory text to be discarded silently.
## Logging and recovery

Studio writes structured JSON Lines logs through `IStudioLogService`. Use **Help > Open Logs Folder** to open the current log directory. Logged events include project creation/open/close and transition decisions, save/save-as results and external-change conflicts, build/clean/cancel transitions, run/stop/external-terminal transitions, editor bridge validation failures and recovery attempts, settings/session corruption recovery, watcher overflow, and unexpected shell failures.

Logs are intended for diagnostics and may include operation names, document IDs, paths, counts, statuses, diagnostics counts, exception types, and short technical details. They must not include complete editor text or complete source-bearing editor protocol payloads.
