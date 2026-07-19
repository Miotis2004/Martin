# Formatter safety contract

The formatter changes only trivia between compiler tokens. It never reconstructs token text,
comments, documentation comments, string contents, or skipped text. It preserves the detected LF or
CRLF convention when `PreserveLineEndings` is enabled and returns minimal `TextEdit` values rather than
a whole-document replacement.

Formatting is refused with diagnostic `MRT6451` when malformed input cannot be changed safely. A
refused result contains no edits, so callers must leave the source untouched. Successful edits are
sorted and non-overlapping. Applying the result twice is a no-op (idempotence).

CLI and Studio both call `MartinLanguageService.GetFormattingEdits`. Studio applies edits only if the
request's workspace, project, document, and Monaco model versions still match. The CLI applies edits
atomically through a temporary file unless operating in check/stdout mode.

Phase 13 adds syntax-aware line boundaries while retaining that contract. Enum cases, protocol
requirements, type members, statements, and switch cases begin on their own indented lines. A switch
case body begins after the pattern's colon. Associated-value lists, nested patterns, optional `.some`
patterns, immutable `let` bindings, conformance lists, `mutating`, and typed `throws` clauses use the
same token-spacing rules as the rest of the language. Tests apply representative pattern and protocol
documents twice to guard idempotence.
