# Martin.LanguageServices dependency direction

The project is transport- and editor-independent. Dependencies flow inward in this order:

1. `Workspace` and `Text` define identity, version, snapshot, position, and edit values.
2. `Features` define feature-specific request and result values and depend only on those shared values and compiler text primitives.
3. `Analysis` coordinates compiler syntax and semantic state and produces feature values.
4. `Abstractions` expose independently consumable feature contracts.
5. `MartinLanguageService` is the compatibility facade over the prototype implementations while each heuristic is replaced in later Phase 12 milestones.

No layer may depend on Studio, WinUI, WebView2, Monaco, command-line handlers, or transport DTOs. Editor adapters own editor-position conversion and stale-result publication.
