# Generated Source Mapping

Generated C# is emitted with `#nullable enable` followed by `#line hidden` so namespace, type, entry-point, runtime wrapper, and compiler-helper scaffolding are hidden from user diagnostics. When a generated statement corresponds to Martin source, the emitter writes a `#line` directive for that source span, records the mapping, emits the statement, and returns to `#line hidden`.

Generated statements that do not correspond to user source, including lowered temporaries and implicit returns, remain hidden.
