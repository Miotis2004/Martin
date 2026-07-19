# Monaco language protocol

Studio's WebView bridge registers completion, hover, definition, references, signature help,
document formatting, and semantic-token providers for the `martin` language. Diagnostics are pushed
as Monaco markers. Provider registrations are disposed during editor recovery and recreated after the
new WebView is ready.

## Request envelope

Every request includes the feature name, document URI/ID, Monaco model version, position or range,
and feature-specific options. Studio maps that model to the current workspace/project/document IDs and
their versions before scheduling work. Positions use zero-based Monaco line/column values at the JSON
boundary and centralized UTF-16 conversion in Studio.

Requests that exceed configured document, change-count, payload, or result limits are rejected with a
controlled protocol error. Monaco cancellation tokens cancel the corresponding Studio request.

## Response rules

Responses carry the workspace, project, and document versions used for calculation. Studio returns a
result only when those versions and the Monaco model version still match. A stale or cancelled request
returns no provider items and never updates markers or semantic tokens. Formatting edits additionally
require the exact model version and are sorted, non-overlapping edits.

Hover documentation is escaped before it enters Monaco Markdown. Error details are logged without
injecting untrusted source into HTML. Live syntax, live semantic, and Build diagnostics retain distinct
sources; duplicate Monaco markers are merged deterministically.

