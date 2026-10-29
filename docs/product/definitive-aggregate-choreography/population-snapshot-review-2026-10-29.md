# PopulationSnapshot aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors, projectors and the `PopulationSnapshotFreezer` are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** One snapshot (`PopulationSnapshot`, stream `population-snapshots/{snapshotId}` under the tenant) decides atomically that a snapshot id freezes exactly one canonical immutable population identity (inline rows, or a manifest over ordered chunk identities), with exact root/amendment lineage and the freezing actor.

- Identity: tenant + `SnapshotId`. Storage chunk streams (`PopulationSnapshotChunk`) are deterministic technical storage with no independent business decision; they stay separate.
- Invariant: one immutable content hash, lineage and actor per snapshot id; amendments are separate snapshots with their own root linkage and never change the original.
- Exclusions: source capture, authorization, row assembly, chunk storage, manifest-commit orchestration and retry recovery (`PopulationSnapshotFreezer`), reading and verifying chunks (`PopulationSnapshotContent`), directory projection.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Freeze (valid inline) | registration | `PopulationSnapshotFrozen` |
| none | FreezeChunked (valid manifest) | registration | `PopulationSnapshotFrozen` (no rows, stored chunk count, manifest hash) |
| none | either, invalid kind, hash, rows, chunks or linkage | validation refusal | none |
| frozen | exact retry (same lineage, kind, hash, reason, actor, manifest) | original registration | none |
| frozen | changed content, lineage, actor, or inline vs chunked form | conflict | none |

## Changes (real gaps)

- `FreezeChunked` trusted caller-supplied digest and chunks. A null chunk, null or invalid kind, malformed digest or chunk hash (`Convert.FromHexString` would throw), inconsistent `ChunkCount`, non-contiguous `Start`, empty or oversized chunk, or chunk over the byte budget could commit a manifest, or throw instead of refusing. It now refuses all of these as validation with no event. Output of `PopulationSnapshotStorage.Plan` and `PopulationContentIdentity.Compute` satisfies every check.
- No other aggregate change. Inline freeze already validated hash, kind, bound, linkage, and exact retry.

## Test-to-criterion table

New file `PopulationSnapshotTests.cs`; the existing `PopulationSnapshotChunkingTests.cs` covers the freezer, storage and read path and is unchanged.

| Criterion | Test |
|---|---|
| Valid inline freeze, payload | `ShouldFreezeInlineGivenValidPopulation` |
| Amendment lineage | `ShouldFreezeAmendmentGivenLineageToOriginalRoot` |
| Valid chunked manifest | `ShouldFreezeManifestGivenOrderedStorageChunks` |
| Invalid decisions append nothing | hash mismatch, invalid kind, rows beyond inline bound, inline over one chunk, original rooted elsewhere, amendment without reason, self amendment, unordered chunks, single chunk, malformed chunk digest, invalid chunked kind, inconsistent chunk count |
| Exact retry (after replayed history) | `ShouldReturnOriginalRegistrationWithoutFactGivenExactInlineRetry`, `...GivenExactChunkedRetry` |
| Changed-intent refusal | different actor, changed content, changed lineage, altered manifest, inline retry of chunked snapshot |
| Replay of valid history | the retry tests seed via `NativeFacts.Raised` and `Given` |
| Stale revision / competing appends | Portia expected-position append OCC (framework); the freezer resolves via retry |
| Bounded history | one event per snapshot; `MaximumInlineRows`, chunk bounds and `MaximumChunks` tests above |
| Interrupted manifest, chunk storage | existing `PopulationSnapshotChunkingTests` |

Malformed-history tests were not added, by user preference.

## Event contract status

- `PopulationSnapshotFrozen` carries business facts only (identity, lineage, content identity, rows or manifest, actor, time). No add, rename, remove or payload change requested.
- Catalog `eventAuthority` is "requires schema reconciliation"; that stays with the shared schema owner.

## Later-stage follow-ups (not aggregate gaps)

- Selected `FreezePopulationSnapshot` receiver command (CommandId identity, reactor origin) and the move from request-id keyed identity to the selected `SnapshotId` binding belong to handler/freezer work.
- Freezer callers (`AcceptAccessPopulation`, campaign launch/closure, workforce roster freeze/amend) own authorization before replay.
- Recovery after chunks stored without manifest is retry-based; a recovery reactor is not in scope.

## Evidence

- Jev: no request this round; decisions follow the catalog invariant and rationale.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~PopulationSnapshot"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: freezer and projection tests are unverified against the stricter chunk validation, which accepts all planner output by construction.

## Review before build

Independent review, nothing compiled or run. Evidence: `implementation-jev/populationsnapshot-test-impl-review-2026-10-29-request.json` and `-response.json` (model `jev-1.14.0`).

### Findings and fixes

- Implementation: every accepted decision enforces the invariant; no stale, duplicate or replayed input appends a second fact (exact retry acknowledges with no event; any difference conflicts). No code change needed.
- Test gap fixed: the review table claimed non-contiguous start and oversized chunk coverage that no test had. Added `ShouldRejectChunkedFreezeWithoutFactGivenNonContiguousChunkStart` and `...GivenChunkOverByteBudget`.
- Compile read: usings, record `with` on `PopulationStorageChunk`/`PopulationDigest`, internal `MaximumInlineRows`/`PopulationContentIdentity` (InternalsVisibleTo Tests), `NativeFacts.Raised`, `Uuid.Parse` with culture all confirmed by reading; not compiled.
- Known limit: the aggregate cannot verify `digest.Sha256` against chunk digests without rows; it trusts the caller digest (freezer computes it). Not an aggregate gap.

### Jev (advisory)

| Question | Answer | Probability |
|---|---|---|
| Identity: catalog says context.RequestId, code uses SnapshotId; disposition | keep SnapshotId, defer request-id binding to freezer/handler stage | 1.00 |
| Premise: a caller-supplied deterministic SnapshotId resolves request-id binding without changing invariants | holds, weak | 0.59 (low confidence, unresolved) |
| Refusing malformed or non-contiguous manifests with no event | correct | 1.00 |
| Tests assert behavior and can fail | true | 0.96 |

Identity discrepancy: disposition is decisive (keep, defer); the supporting premise is low confidence and stays flagged as a handler-stage item. No event or catalog change requested. Payload `TenantId`/`SnapshotId` in `PopulationSnapshotFrozen` repeat stream identity; changing that is a shared event contract matter and is reported, not applied.

### Remaining risks

- Written, not executed; build and tests pending.
- `PopulationSnapshotChunkingTests` not run against stricter validation (planner output satisfies it by construction).
- Request-id vs SnapshotId binding and event payload identity duplication belong to the shared schema/freezer owner.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
