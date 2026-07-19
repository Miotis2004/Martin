# Martin Studio Build and Run

Studio builds and runs projects through library services, not through CLI process scraping. The build path uses `Martin.Build`; the run path uses `Martin.Execution`.

## In-memory snapshots

Builds capture immutable workspace snapshots. Open source files use current editor text, including unsaved changes. Closed source files use disk text. Non-source files are excluded from compilation. Each snapshot captures document versions and the workspace generation so stale async results can be rejected.

Snapshot order is deterministic. Read failures are returned as Studio diagnostics rather than crashing the shell.

## Build command

The Build command validates the workspace, captures a snapshot, updates command state, records output, calls the build service, maps diagnostics to snapshot versions, rejects stale diagnostics, updates the Error List and editor markers, reports artifacts, and restores command state.

Cancel Build cancels the active service token, reports cancellation without treating it as an internal error, preserves prior output where possible, and leaves the workspace usable.

## Output layout

Studio follows the project build policy from Phase 10:

```text
<project-root>/<build.output>/<Configuration>/<TargetFramework>
```

Studio must not guess artifact paths from filenames.

## Run

Run first builds the current in-memory workspace. If the build fails, execution is skipped. On success, Studio executes the validated result and captures stdout, stderr, exit code, and stop state in the Program output channel.

## Run Without Build

Run Without Build is available only when no project source document is dirty and published build state is fresh according to Phase 10 freshness rules. It must not execute an artifact based only on file existence.

## External terminal

Run in External Terminal is a Studio-specific Windows service for interactive programs. It preserves arguments, sets a working directory, avoids command injection, prefers Windows Terminal when available, and falls back to Command Prompt. External launches are detached after startup, so Stop controls integrated execution only and Studio reports launch success or startup failure rather than the detached process exit code.

## Output channels

Output channels are Studio, Project System, Build, Compiler, and Program. Build and Program output are independently selectable. Program output should not be rewritten line-by-line by Studio.
## Logging and recovery

Studio writes structured JSON Lines logs through `IStudioLogService`. Use **Help > Open Logs Folder** to open the current log directory. Logged events include project creation/open/close and transition decisions, save/save-as results and external-change conflicts, build/clean/cancel transitions, run/stop/external-terminal transitions, editor bridge validation failures and recovery attempts, settings/session corruption recovery, watcher overflow, and unexpected shell failures.

Logs are intended for diagnostics and may include operation names, document IDs, paths, counts, statuses, diagnostics counts, exception types, and short technical details. They must not include complete editor text or complete source-bearing editor protocol payloads.
