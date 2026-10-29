# ClientExport aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain as is).** `ClientExport` (stream `client-exports/{exportId}` under the tenant) owns one export: its frozen source inventory, build attempts, per-package attest delivery decisions and the single final delivery.

- Identity: tenant + `exportId`. Command ids and attempt ids never fork the stream; attempt ids are checked inside the stream.
- Invariant: final export delivery requires a successful build whose counts match the frozen included/withheld inventory and, for each required attest package, a latest successful delivery against the exact manifest hash; final delivery is immutable and exact.
- Exclusions: archive building and attest delivery execution (workers/executors), authorization of who may export, the retention/hold determination that produced the frozen inventory (source facts), export views.
- The catalog marks the boundary as `process` for the case where executor and attest delivery lifecycles later split; today both are decided in one stream, so no split. The all-required-deliveries gate stays here.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Request valid inventory | requested, revision 1 | `ClientExportRequested` |
| none | Build result / attest decision / Deliver | not found | none |
| requested, build_failed | Build result (next attempt, exact counts) | built or build_failed | `ClientExportBuilt` / `ClientExportBuildFailed` |
| any | Request exact retry | no-op | none |
| any | Request changed intent | state conflict | none |
| any | Build result, attempt id already recorded, same content | no-op (before revision check) | none |
| any | Build result, attempt id reused with changed content | state conflict | none |
| any | Stale expected revision | version conflict | none |
| built | Attest decision (delivered with receipt / not_delivered with rationale) | recorded | `ClientExportAttestPackageDeliveryRecorded` |
| any | Attest decision identical to any earlier decision for the package | no-op | none |
| package delivered | Different attest decision | state conflict (immutable) | none |
| built, all packages delivered | Deliver | delivered, complete | `ClientExportDelivered` |
| not built / missing package delivery | Deliver | state conflict | none |
| delivered | Deliver exact retry / changed receipt | no-op / state conflict | none |
| delivered | Build result / attest decision | refused | none |

## Changes (real gaps)

1. Attest decision retry after later facts: the exact-retry check compared only the latest decision for the package, so retrying an earlier committed `not_delivered` after a later `delivered` was refused as "immutable" instead of acknowledged. It now acknowledges an exact match against any earlier decision for the package.
2. Build attempt retry normalization: `SameBuildAttempt` compared the stored (trimmed, lower-cased) failure code and normalized archive hash with the raw command values, so a retry with differently cased input was refused as a reused attempt id. Both sides are now normalized.

Everything else already met the criteria. No malformed-history tests were added (user preference).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveIdentityFromExportIdGivenTenantAndExport` |
| Valid request, reconciliation, rejection of unexplained withholding and duplicates | `ClientExportTests` (existing) |
| Exact retry after later facts (request) | `ShouldAcknowledgeExactRequestRetryWithoutNewFactGivenLaterBuildFact` |
| Changed intent (request) | `ShouldRefuseChangedRequestWithoutNewFactGivenRequestedExport` |
| No fact without a record | `ShouldRefuseBuildResultGivenExportNotRequested` |
| Stale revision | `ShouldRefuseBuildResultWithoutNewFactGivenStaleExpectedRevision`, `ShouldRefuseDeliveryWithoutNewFactGivenStaleExpectedRevision` |
| Build attempt retry after later facts, attempt id reuse | `ShouldAcknowledgeBuildAttemptRetryWithoutNewFactGivenLaterAttempt`, `ShouldRefuseBuildAttemptIdReuseGivenChangedAttemptNumber` |
| Interrupted build, retry | `ShouldKeepInterruptedBuildIncompleteAndAllowExactRetryGivenExportRequest` (existing) |
| Terminal state | `ShouldRefuseBuildResultWithoutNewFactGivenDeliveredExport` |
| Delivery gate (build, attest) | `ShouldRefuseDeliveryWithoutNewFactGivenOnlyFailedBuild`, `ShouldRequireAttestDeliveryDecisionBeforeFinalExportDeliveryGivenCollaborationBundle`, `ShouldAllowSuccessfulAttestDeliveryAfterRecordedInterruptionGivenCollaborationBundle` (existing) |
| Final delivery exact retry / immutable | `ShouldAcknowledgeExactDeliveryRetryAndRefuseChangedReceiptGivenDeliveredExport` |
| Attest retry after later facts, immutability, unknown package, receipt required | `ShouldAcknowledgeEarlierAttestDecisionRetryWithoutNewFactGivenLaterDeliveredDecision`, `ShouldRefuseChangedAttestDecisionWithoutNewFactGivenDeliveredPackage`, `ShouldRefuseAttestDecisionWithoutNewFactGivenPackageNotRequiredByExport`, `ShouldRefuseDeliveredAttestDecisionWithoutNewFactGivenMissingReceipt` |
| Replay of valid history, continuation | `ShouldReplayBuildAndDeliveryHistoryGivenRetainedExportEvents` (existing), `ShouldRestoreStateAndAcceptDeliveryGivenReplayedBuiltHistory` |
| Bounded history | enforced in source (`MaximumBuildAttempts`, `MaximumAttestDeliveryAttempts`, `MaximumEventBytes`); no new test |
| Competing decisions | expected-revision check plus framework OCC on append |

## Event contract status

- Five events: `ClientExportRequested`, `ClientExportBuilt`, `ClientExportBuildFailed`, `ClientExportAttestPackageDeliveryRecorded`, `ClientExportDelivered`. Catalog `eventAuthority` is `retained_baseline_business_facts_requires_schema_reconciliation`.
- Each event carries `TenantId`, `SubjectId`, `Revision`, `ActorMemberId`, `OccurredAt` plus the full `ClientExportView` snapshot, which repeats those fields and all prior history. This duplicates metadata and grows with attempts. Recorded as a schema-reconciliation item for the event owner; not changed here (shared event contracts).
- Catalog commands `RecordOwnAttestRecordDelivery` / `RecordOwnAttestRecordPackageResult` / `RequestOwnAttestRecordPackage` belong to the attest-record side and are not implemented on this aggregate; no change.

## Later-stage follow-ups (not aggregate gaps)

- Command-id acceptance/receipts (`CommandId` logical identity) in the native operation executor and handlers; the aggregate takes expected revisions directly.
- Schema reconciliation: delta payloads instead of full-state snapshots, drop metadata duplication.
- Export build and attest delivery reactors; export views and routes.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ClientExport"`.
- RED: none captured; not claimed.
- Residual risk: new test file and the two source edits are uncompiled; `Given` is fed pending events with metadata, mirroring the existing replay test.

## Review before build

Independent review, nothing built or run. Source, tests and diff were re-read; the implementer summary was not trusted.

### Findings

- Implementation: the two source edits (attest retry against any earlier decision; normalized build-attempt retry) were read as the compiler would and are type-correct (`string?` ternary, `FailureCode?.Trim().ToLowerInvariant()`). They enforce the invariant: delivered attest decisions stay immutable, exact retries append nothing, the delivery gate still requires a latest built attempt and a latest delivered exact-manifest decision per package. No stale, duplicate or competing input can append a wrong fact (expected revision is checked after retry acknowledgement; framework OCC covers competing appends).
- Tests: `ClientExportLifecycleTests` asserts failure codes, revision and pending event counts, and replay continuation, so each can fail. Names follow CNTRYL0001 and every test has ordered Arrange/Act/Assert. Fixture ids are `Uuid.CreateVersion4()`, hashes are 64 hex characters, counts match the frozen inventory (1 included, 1 withheld). Overload shapes match `ClientExportBuildResultContent` as used by the existing `ClientExportTests`. `ShouldDeriveIdentityFromExportIdGivenTenantAndExport` is weak but valid; kept.
- Compile risks: none found. Replay seeds `Given` with pending events exactly as the committed `ClientExportTests` replay test does. Not confirmed without a compiler: `IDE0055` alignment of the multi-line collection expression in `Content()` (copied from the existing style).
- No fixes were required in this review.

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/clientexport-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer | Probability |
|---|---|---|
| Retry of earlier committed attest decision after later delivered decision | acknowledge with no event | 0.86 (confidence 0.79) |
| Delivery gate enforces the invariant (noul) | true | 0.79 |
| Event payload repeating metadata and full state | report for schema owner, do not change in aggregate | 0.98 |
| Tests assert behavior and can fail (noul) | true | 0.95 |

### Discrepancies and disposition

- Event payloads carry the full `ClientExportView` snapshot repeating tenant, export id, revision, actor and prior history. Jev: report for the schema owner (0.98). Decisive; no aggregate change; listed under event changes needed.
- No event or command in code lacks the catalog, or the reverse; the five retained baseline events are unchanged.

### Remaining risks

- Build and test pending; all new code uncompiled.
- Delta-payload schema reconciliation open (shared schema owner).
- Command-id acceptance, handlers, reactors, views and routes are later stages.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
