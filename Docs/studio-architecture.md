# Martin Studio Architecture

Martin Studio is the WinUI 3 IDE for single Martin projects. The current Phase 11 implementation is a hardened prototype: it uses a testable `Martin.Studio.Core` layer for workspace state, document state, editor protocol primitives, view models, output, diagnostics, build coordination, execution coordination, settings, sessions, logging, and recovery. `Martin.Studio.WinUI` owns WinUI composition, XAML, assets, and visual hosting.

## Dependency direction

```text
Martin.Studio.WinUI -> Martin.Studio.Core -> Martin.ProjectSystem
                                      | -> Martin.LanguageServices
                                      | -> Martin.Build
                                      | -> Martin.Execution
                                      | -> Martin.Compiler
```

Studio code must not implement IDE behavior by launching `martin` or `martinc` and parsing their output. Project discovery and validation flow through `Martin.ProjectSystem`; builds flow through `Martin.Build`; execution flows through `Martin.Execution`.

## Core responsibilities

`Martin.Studio.Core` contains:

- Workspace state: active project, project tree, open documents, diagnostics, build state, execution state, and output.
- Service contracts: workspace, settings, session, recent projects, output, diagnostics, build, execution, logging, recovery, and project-opening services.
- View models: shell, project explorer, document tabs, output pane, error list, and status bar.
- Editor protocol types: JSON envelopes, payload records, request tracking, bridge state, validation, and recovery hooks.
- Editor asset validation helpers for locally bundled Monaco assets.

Core must remain UI-independent. Do not expose `Window`, `FrameworkElement`, `ContentDialog`, `StorageFile`, `WebView2`, `DispatcherQueue`, or XAML controls from Core APIs.

## WinUI responsibilities

`Martin.Studio.WinUI` contains:

- `App.xaml` and `App.xaml.cs` composition root.
- `MainWindow.xaml` and code-behind for visual shell wiring.
- Assets under `Assets/`, including editor assets.
- Converters and WinUI-only services.

WinUI code may host WebView2, menus, command bars, dialogs, and platform-specific services, but should delegate workspace, document, build, run, settings, and diagnostics behavior to Core services and view models.

## MVVM and commands

The shell is coordinated by `MainWindowViewModel`. Child view models own Project Explorer, document tabs, output, errors, and status. Commands are exposed from view models so menus, toolbar buttons, and keyboard accelerators can share command state.

Command enablement is derived from workspace state: project loaded, active dirty document, build running, execution running, diagnostics available, and editor readiness where applicable.

## Diagnostics ranges

Studio-reserved diagnostics are:

- `MRT5000-MRT5099`: workspace, project, and document
- `MRT5100-MRT5199`: build and execution coordination
- `MRT5200-MRT5299`: editor bridge and WebView2
- `MRT5300-MRT5399`: settings, session, and theme
- `MRT5400-MRT5499`: shell and UI services

## Known limitations

- Martin Studio is a single-project IDE.
- Advanced semantic completion, hover, go-to-definition, rename, debugger support, plugins, and integrated terminal emulation are deferred to later phases.
- The current Studio implementation is being hardened toward alpha and may still lack some production shell behaviors described in the Phase 11 guide.
## Logging and recovery

Studio writes structured JSON Lines logs through `IStudioLogService`. Use **Help > Open Logs Folder** to open the current log directory. Logged events include project creation/open/close and transition decisions, save/save-as results and external-change conflicts, build/clean/cancel transitions, run/stop/external-terminal transitions, editor bridge validation failures and recovery attempts, settings/session corruption recovery, watcher overflow, and unexpected shell failures.

Logs are intended for diagnostics and may include operation names, document IDs, paths, counts, statuses, diagnostics counts, exception types, and short technical details. They must not include complete editor text or complete source-bearing editor protocol payloads.
