# ControlOccurrence aggregate review

## Identity, invariant and boundary

- **Identity:** (tenantId, programId, controlId, businessWindow) via `ControlOccurrenceIdentity.Of`; stream area `control-occurrences`. Generator, plan version, period text and trigger text never enter the identity, so no caller key forks the stream.
- **Named invariant (catalog):** one occurrence per control performance window, with its single materialization, attestation versions and review decisions as one ordered, revision-guarded history. Review sign-off content rules (frozen evidence set, outcome, rationale) are decided by the caller from `ControlOccurrenceState` and re-checked here against the revision (D19-02, D19-03, D19-05).
- **Exclusions:** operating plan approval (`ControlOperatingPlan`), generator selection and fan-out (reactor), attestation/review content validation and authorization (handlers), readiness and monitoring data (other owners), projections of occurrence views.
- **Decision:** keep as one aggregate. Materialization, attestation versions and review are one per-window lifecycle; history is bounded by one window's corrections.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| none | `Open` / `Materialize` / `Attest` with materialization | `ControlOccurrenceOpened` (+ `Attested` in the batch) | wrong owner stream or missing expected revision (Validation); stale revision |
| none | `Attest` / `Review` without materialization | none | NotFound |
| Opened | `Open` / `Materialize` | none | already materialized (Conflict) |
| Opened / Attested | `Attest` at current revision | `ControlOccurrenceAttested` | stale revision |
| Opened (no attestation) | `Review` | none | Conflict (new) |
| Attested | `Review` at current revision | `ControlOccurrenceReviewed` | stale revision |

## Changes (real gaps)

- `Review` on an opened occurrence with no attestation passed `PrepareProof` and then threw `InvalidOperationException` from the replay guard inside `RaiseEvent`. It now returns a Conflict failure first and appends nothing.

## Test-to-criterion table

Existing handler-level coverage lives in `ControlOccurrenceTests` and `ControlOccurrenceFanOutTests`; new owner-level tests are in `ControlOccurrenceAggregateTests`.

| Criterion | Tests |
|---|---|
| Stable identity | `ShouldAddressStreamByTenantProgramControlAndWindowGivenAnyOpener`, `ShouldDeriveOneIdentityPerControlAndBusinessWindowGivenAnyGeneratorOrTrigger` |
| Success path | `ShouldMaterializeAndAttestInOneBatchGivenExpectedOccurrenceNotYetOpened`, `ShouldKeepEveryAttestationVersionAndReviewGivenCorrectionHistory`, `ControlOccurrenceTests` flows |
| Invalid decision appends nothing | `ShouldAppendNothingGivenAttestationOfUnmaterializedOccurrenceWithoutMaterialization`, `ShouldAppendNothingGivenReviewBeforeAnyAttestation`, `ShouldRefuseGivenOperationNotRootedOnOwnStreamOrWithoutExpectedRevision` |
| No-op / exact retry | materialization is once per window: `ShouldMaterializeOnceGivenOpenAndRefuseSecondOpening`, `ShouldReturnOriginalFactGivenSecondGeneratorSelectingSameWindow`, `ShouldResumeWithoutDuplicateOccurrencesGivenCrashAfterSomeReceivers`; operation-level retry is the native acceptance seam |
| Changed intent | `ShouldConflictGivenChangedFrozenTermsForSameWindow` |
| Stale revision / competing | `ShouldAppendNothingGivenStaleExpectedRevisionOnAttestationAndReview`, `ShouldRejectSecondAttestationGivenConcurrentSubmission` |
| Replay | `ShouldRestoreStateGivenValidHistoryAndRefuseStaleCommandAfterReplay` |
| Bounded history | one opening plus attestation versions and reviews of a single window |

## Event contract status

`ControlOccurrenceOpened`, `Attested` and `Reviewed` carry business facts and content references; provenance is the native-operation envelope used across native owners. No event change needed.

## Later-stage follow-ups (not aggregate gaps)

- The catalog invariant (review requires a frozen evidence set binding readiness assessment version, monitoring measure versions and agenda/minutes artifact; outcome approved/action_requested/deferred) is enforced by the review handler over `ControlOccurrenceState`; confirm at the handler stage.
- D19-05 (one annual policy/vendor review occurrence per policy or material provider) is decided by window selection in the generator reactor.

## Evidence

Tests written, not executed; build and run pending. Source change written with its test (RED not run). Nothing was built, formatted or committed. Jev not consulted (no event naming change). Risk: any caller that reviewed an unattested occurrence previously got an exception and now gets a Conflict.

## Review before build (2026-10-29)

Independent review; nothing built, run, formatted or committed.

### Findings and fixes

- **Compile error (fixed):** `Pending` returns `DomainEvent[]`; the tests used `.Count`, which does not exist on arrays. Replaced with `.Length` and `Assert.Single` (xunit2013) in `ControlOccurrenceAggregateTests`.
- **Compile risk (fixed):** `Assert.Equal([1L, 2L, 3L, 4L], ...)` relied on collection-expression overload inference; replaced with an explicit `new[] { 1L, 2L, 3L, 4L }`.
- Implementation reviewed: every accepted `Open`, `Materialize`, `Attest` and `Review` is guarded by owner stream, expected revision, materialization and (review) prior attestation; the Review guard precedes `RaiseEvent`, so no exception path remains. No change needed.
- Fixtures checked against types: windows valid (From < Until), terms window equals the owner window so replay identity holds, provenance rooted on the owner stream, usings present.
- Tests assert behavior (events appended or not, revision, replay outcomes); none structural-only. Phase comments and names follow CNTRYL0001/0002 (some setup `Assert.True` calls sit in Act of the history test; allowed).

### Jev (model jev-1.14.0)

Request/response: `implementation-jev/controloccurrence-test-impl-review-2026-10-29-*.json`.

| Question | Answer | Probability |
|---|---|---|
| Review before any attestation: conflict_no_event / throw / needs_source_resolution | conflict_no_event | 1.00 / 0.00 / 0.00 |
| Premise: catalog silent on review without attestation | true | 0.95 |
| Payload repeats stream identity: keep_as_cataloged / remove / needs_source | keep_as_cataloged | 0.92 / 0.06 / 0.02 |
| Review-before-attestation test asserts invariant | true | 0.91 |
| Stale-revision test asserts invariant | true | 0.87 |

Dispositions: the Conflict refusal is applied (decisive). Payload identity fields stay as cataloged; no event change. Both tests judged sound.

### Remaining risks

- Not compiled or run. Handler enforcement of the frozen evidence set is a later stage. Opened carries `creationIntent` twice (existing baseline payload); not changed here.


## Coordinator build and test evidence (2026-10-29, wave 3)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7231 passed, 5 failed of 7236.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
