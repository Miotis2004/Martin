# Martin Studio Settings and Session

Studio stores user settings and session state outside Martin project directories. Storage must work for packaged and unpackaged WinUI execution.

## Settings

Settings are versioned JSON and saved atomically. Corrupt settings should produce a diagnostic and fall back to defaults without blocking startup.

Settings include:

- Theme: System, Light, or Dark
- Reopen last project
- Restore open documents
- Maximum recent projects
- Maximum output entries
- Generated-folder visibility
- Clean-document auto-reload behavior
- Window placement behavior

Do not store source text in settings.

## Session

Session state is also versioned JSON and atomically saved. Session data includes project manifest path, open documents, active document, cursor and scroll positions, selection, Project Explorer expansion, bottom-panel tab and visibility, panel sizes, window placement, and maximized state.

Do not store ordinary source text in session files. Unsaved source recovery is out of scope for Phase 11.

## Theme behavior

System, Light, and Dark themes update WinUI, Monaco, icons, severity visuals, and syntax colors. Theme changes must not reopen documents, recreate the workspace, lose undo history, or clear view state. High contrast settings must remain usable and must not rely on color alone.
