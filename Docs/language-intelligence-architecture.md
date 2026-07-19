# Language intelligence architecture

Phase 12 language intelligence is a reusable compiler-backed subsystem. `Martin.LanguageServices`
does not depend on Studio, Monaco, WebView2, or the command line. Studio owns transport and editor
lifecycle; the compiler remains the authority for declarations, references, types, and signatures.

## Data flow

1. `LanguageWorkspace` discovers every project source and publishes immutable snapshots.
2. Open editor buffers overlay disk documents while preserving stable project and document IDs.
3. `AnalysisScheduler` prioritizes requests, coalesces duplicates, limits concurrency and queue size,
   and cancels superseded document/project work.
4. `LanguageAnalysisCache` keys document and project analysis by stable ID and exact version. Its LRU,
   entry, and approximate-memory limits prevent unbounded history.
5. Feature providers consume syntax trees, compilations, semantic models, declaration indexes, and
   reference indexes. They return versioned, editor-independent contracts.
6. Studio validates the response version before publishing diagnostics or returning a Monaco result.

Published snapshots and results are immutable. Cache leases prevent eviction while a result is being
used. Project close cancels scheduled work and clears retained analysis; shutdown awaits workers.

## Identity and version rules

Workspace, project, document, and symbol identities are stable within their documented lifecycle.
Versions are monotonically increasing value types. Every interactive request names all four IDs plus
workspace, project, and document versions. A mismatch before or after analysis produces a stale result;
stale results must never update editor state.

Cancellation is expected control flow. Monaco cancellation reaches Studio, then the scheduler, and
finally compiler-backed providers. A newer position-sensitive request cancels older work for that
document. Cancellation is not presented as a diagnostic or user-facing failure.

## Semantic feature boundary

Completion may add a fixed keyword/snippet catalog, but symbol candidates are semantic. Hover,
definition, references, signature help, and semantic classification resolve compiler symbols rather
than regexes or whole-word searches. Declaration and reference indexes use stable `SymbolId` values.
The same formatting service serves CLI and Studio.

## Observability

The cache reports hits, misses, evictions, entry counts, estimated bytes, and hit rate. The scheduler
reports queued, started, completed, cancelled, stale, coalesced, and rejected work, total queue latency,
current queue depth, and peak queue depth. These counters are process-local and monotonic, except the
current depth gauge.

