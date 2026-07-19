# Martin Studio Accessibility

Martin Studio accessibility is part of Phase 11 completion hardening. Core workflows must be usable by keyboard and understandable to assistive technologies.

## Keyboard access

Required shortcuts:

| Shortcut | Action |
|---|---|
| Ctrl+O | Open Project |
| Ctrl+S | Save |
| Ctrl+Shift+S | Save All |
| Ctrl+W | Close Document |
| Ctrl+B | Build |
| Ctrl+Shift+B | Cancel Build while building |
| F5 | Run |
| Ctrl+F5 | Run Without Build |
| Shift+F5 | Stop |
| Ctrl+` | Toggle Output |
| F8 | Next Diagnostic |
| Shift+F8 | Previous Diagnostic |
| Ctrl+F | Find |
| Ctrl+H | Replace |
| Ctrl+Tab | Next Document |
| Ctrl+Shift+Tab | Previous Document |

Menus, toolbar buttons, and shortcuts should invoke the same commands.

## UI requirements

- Logical tab order.
- Visible keyboard focus.
- Accessible names and descriptions on primary controls.
- Tooltips for commands.
- Keyboard-operable Project Explorer, tabs, Output, Error List, and dialogs.
- Focus transfer into and out of Monaco/WebView2.
- Sufficient hit targets.

## Non-color cues

Diagnostic severity, dirty state, build state, and execution state must be available as text, not only icons or colors. Error List entries should be screen-reader friendly and include severity, code, message, file, line, column, and source.

## High contrast

High contrast must preserve readable text, visible focus, and distinguishable diagnostic state. Monaco theme synchronization must not override system accessibility needs.


## Help and diagnostics access

The Help menu exposes **Open Logs Folder** so keyboard users can reach diagnostic logs without browsing to an application-data path manually. Error List entries, Output entries, status messages, and failure dialogs should include actionable text that identifies the failed command and next step.
