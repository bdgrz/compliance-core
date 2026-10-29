# AdvisorFeedbackThread aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep as the catalog's selected split target (`selected_split`).** One thread (stream `advisor-feedback/{identity}` under the tenant) decides atomically the immutable contribution materialized from the handoff admission and its correction, response and resolution lifecycle. The handoff never loads the thread and the thread never loads the handoff, so revoking or closing the handoff never rewrites feedback.

- Identity: tenant + program + handoff + feedback (`AggregateIdentity.Create`). The feedback id is the admission's logical id; `CorrectionId` identifies each correction operation; other mutations bind `CommandId` to this identity and the expected revision (`OperationProvenance`, `NativeAcceptance`, `AdvisorReplayCut`). No caller key outside the identity forks a stream.
- Invariant: feedback is a `comment`, `change_request` or `validated` entry on an exact record version, attributed at admission; a posted entry is never edited, a correction is a new linked fact, the thread is resolved at most once, and every fact advances the logical revision by one from revision one at materialization.
- Exclusions: handoff admission authority, the frozen admission intent and generation (handoff), materialization reactor and receipt (`AdvisorFeedbackMaterializationReactor`), authorization (personal HTTP actor, Compliance Lead), content text normalization, listing/work-item projections.
- Split/reactor decision: no further split. The reactor materializes from the admitted root fact; that coordination stays outside the aggregate.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (valid kind, own scope, own stream) | recorded, revision 1 | `AdvisorFeedbackRecorded` |
| none | Materialize (kind outside catalog, other feedback scope, empty record id/version, foreign stream) | validation refusal | none |
| none | Correct, Respond, Resolve | not found | none |
| recorded | Materialize again | conflict | none |
| recorded | Correct / Respond / Resolve with stale revision | conflict | none |
| recorded | Correct with empty correction id, or operation id differing from the correction id | validation refusal | none |
| recorded | Correct / Respond / Resolve without an expected revision or off-stream | validation refusal | none |
| recorded | Correct / Respond (current revision) | revision + 1 | `AdvisorFeedbackCorrected` / `AdvisorFeedbackResponded` |
| recorded | Resolve (current revision) | revision + 1 | `AdvisorFeedbackResolved` |
| resolved | Resolve again | conflict | none |

Exact retry (including retry of a committed fact after later facts) and changed intent are decided by the operation (`NativeAcceptance`, `AdvisorReplayCut`) and handler receipts; see the existing suites.

## Changes (real gaps)

- `Materialize` did not check that the admitted contribution belongs to this thread (tenant, program, handoff, feedback), that `Kind` is one of the catalog kinds, or that the record and version identity are present. The handler checks most of this earlier, but the aggregate would still append a fact carrying a kind the catalog does not allow. It now refuses these as validation before appending.
- `Correct` accepted an empty `CorrectionId` and an operation whose logical id differs from the correction id. The catalog makes `CorrectionId` the logical identity of the correction, so the aggregate now refuses both as validation.
- No gap found in identity derivation, revision/stale handling, once-only resolution, or replay (replay already throws for out-of-scope or duplicate facts).
- Not changed (undecided in the catalog): whether a correction or response may follow a resolution. The aggregate allows it today; recorded as an open product question, not enshrined by a test.

## Test-to-criterion table

New file `AdvisorFeedbackThreadAggregateTests.cs`; the existing `AdvisorFeedbackThreadCommandTests` (handler-level) stay unchanged.

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveStableIdentityGivenSameTenantProgramHandoffAndFeedback` |
| Valid materialization, payload, revision one | `ShouldAppendRecordedFactAtRevisionOneGivenAdmittedContribution` |
| Invalid decision appends nothing (kind) | `ShouldRefuseMaterializationAndAppendNothingGivenKindOutsideTheCatalog` |
| Invalid decision appends nothing (scope, record identity, stream) | `...GivenContributionOfAnotherFeedback`, `...GivenEmptyRecordIdentity`, `...GivenOperationOnAnotherStream` |
| Second materialization refused | `ShouldRefuseSecondMaterializationGivenThreadAlreadyRecorded` |
| Refusal with no fact | `ShouldReportNotFoundGivenThreadNeverMaterialized` |
| Stale / competing revision after later facts | `ShouldRefuseWriteGivenStaleExpectedRevisionAfterLaterFacts` |
| Resolved once | `ShouldRefuseResolutionGivenThreadAlreadyResolved` |
| Correction identity | `ShouldRefuseCorrectionAndAppendNothingGivenEmptyCorrectionIdentity`, `...GivenOperationOfAnotherLogicalId` |
| Root operation with expected revision | `ShouldRefuseWriteAndAppendNothingGivenOperationWithoutExpectedRevision` |
| Replay of valid history, original contribution preserved | `ShouldKeepOriginalContributionAndAdvanceRevisionGivenReplayOfCorrectionResponseAndResolution` |
| Exact retry after later facts, changed intent, unknown append outcome, handoff revoked, program scope | existing `AdvisorFeedbackThreadCommandTests`, `AdvisorNativeHistoryTests` |

Direct aggregate success of Correct, Respond and Resolve after the first fact needs an executor-finalized native cut, so those are covered through handler tests; owner tests exercise the `Prepare*` decisions and the refusals that precede the cut. Malformed-history tests were not added, by user preference.

## Event contract status

- `AdvisorFeedbackRecorded`, `AdvisorFeedbackCorrected`, `AdvisorFeedbackResponded`, `AdvisorFeedbackResolved` carry business facts plus operation provenance. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: Correct/Respond/Resolve already validate ids; keep handler validation aligned with the new aggregate refusals.
- Product decision to record: may corrections or responses follow resolution?
- Reactor/receipt: `AdvisorFeedbackMaterializationReactor` binding to the admitted root fact (already exists; later-stage verification).
- Projectors: work-item and thread snapshot projections.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and the replay invariants in the source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AdvisorFeedbackThread"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: blind-written test compile errors; existing handler tests that materialize with an unlisted kind or an empty record id (none found by reading) would now get a validation refusal.

## Review before build (independent reviewer, 2026-10-29)

Nothing was built or run. Evidence is by reading source, tests, handler and reader.

### Findings and fixes

- Gap fixed: `Materialize` accepted any non-null `SelectedSource` without checking its identity. A selected source that is unsupported (for example a legacy pair with an empty version) would have been recorded. It now mirrors `AdvisorFeedbackReader`: the effective source (selected, else the record/version pair) must have a valid identity and a selected source must be supported, and the record id must be present. Added `using ...AdvisorCollaboration.Models` to the source for `AdvisorRecordVersion`.
- Test added: `ShouldRefuseMaterializationAndAppendNothingGivenEmptyRecordIdentity` now also covers an unsupported selected source (fails against the previous logic).
- Readability: the previous `a || b || c && d` condition was replaced; no behavior change.
- Checked, no change: scope/kind/record refusals match the reader and handler (`MaterializeAdvisorFeedbackHandler` already permanent-fails on a foreign scope before calling the aggregate), so no valid existing flow now fails. Test constructors, event argument orders (Recorded 13, Corrected 11, Responded 10, Resolved 9), `ResolvedBy`, `Revision` (long vs int literal infers `long`) and `Assert.Single` usage were read as the compiler would and look correct. Replay history uses `DomainEventSeed.Attach` on fresh events only.

### Jev (jev-1.14.0; request and response in `implementation-jev/advisorfeedbackthread-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Corrections/responses after resolution: leave unchanged and record an open question, refuse now, or needs source resolution | leave_unchanged_record_open_question | 0.99 |
| Materialize validations consistent with the catalog rule and invent nothing beyond it | not decisive | Noul 0.37 |
| Tests assert behavioral outcomes covering the rule and code behavior | moderate | Noul 0.68 |

Dispositions: the after-resolution question is decisive, so behavior stays unchanged and it remains an open product question (no event or catalog change). The 0.37 answer is low confidence, not approval; it was checked against source: the kind set and exact-version attribution come from the catalog, the scope and stream checks are identity guards that the existing reader and handler already enforce, so they are kept and flagged as extra-catalog guards. The 0.68 shows the success paths of Correct/Respond/Resolve are not covered at owner level (needs an executor-finalized cut); this stays recorded.

### Remaining risks

- Not compiled; the success path of a supported native `SelectedSource` is not covered by an owner test (constructing a valid native source blind was not attempted).
- Open product decision: corrections or responses after resolution.


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
