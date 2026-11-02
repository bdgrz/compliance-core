# ArtifactRetentionBasis aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep.** One immutable seven-year basis per exact tenant content identity (`artifact-retention-bases`, id derived from tenant, source kind, source id and content SHA-256).

- Identity: `Tenant / artifact-retention-bases / AggregateIdentity("ArtifactRetentionBasis", tenant, kind, sourceId, sha256)`. `selectedLogicalId` only selects the operation for native replay; it never forks the stream.
- Invariant: default retention is the period end plus seven years (`RetainsThrough = PeriodEnd + 7y`); the basis kind must match the source kind (`evidence_artifact` -> `source_period` using the source's own period; `application_import` -> `admin_period` with an explicit period). Inconsistent end date or kind is refused. One basis per content identity, never revised.
- Exclusions: legal holds (`ArtifactLegalHold`), engagement holds, reliance, disposition, source artifact state (supplied as an `ArtifactRetentionSourceSnapshot`), legacy `ArtifactRetentionBound` migration.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Record, exact source, valid period and reason, expected 0 | recorded, rev 1 | `ArtifactRetentionBasisRecorded` |
| recorded | exact retry of the accepted operation | success | none |
| recorded | same operation with changed intent / actor / digest | conflict | none |
| recorded | different operation, any expected revision | conflict | none |
| none | stale revision, source mismatch, position 0, bad period, bad reason, foreign or mis-kinded operation | conflict or validation | none |

## Changes

No real gap found in the aggregate; no source change. The existing aggregate tests (`ArtifactRetentionTests`) covered the happy path, changed intent, changed actor, missing period, identity and payload bound. The following criteria had no aggregate-level test and were added.

## Test-to-criterion table (`ArtifactRetentionBasisTests`)

| Criterion | Test |
|---|---|
| Exact retry appends nothing | `ShouldAcknowledgeExactRetryWithoutNewFactGivenRecordedBasis` |
| Competing / second decision, stale revision | `ShouldRefuseSecondDecisionGivenRecordedBasisAndDifferentOperation` |
| Invariant: source-period basis, trimmed reason | `ShouldDeriveSourcePeriodBasisGivenEvidenceArtifactWithoutExplicitPeriod` |
| Invariant: admin period, leap-day end | `ShouldRecordAdminPeriodBasisGivenApplicationImportExplicitPeriod` |
| Evidence period must match native source | `ShouldAppendNothingGivenEvidencePeriodDisagreeingWithSource` |
| Invalid decisions append nothing | `ShouldAppendNothingGivenInvalidDecision` |
| Replay of valid history, retry and changed intent after replay | `ShouldRestoreBasisAndAcceptRetryGivenValidHistoryReplay` |
| Stable identity | `ShouldKeepStreamIdentityGivenDifferentSelectedOperations`, `ShouldRefuseIdentityGivenInexactSourceContent`, existing `ShouldUseDifferentBasisOwnersGivenDifferentExactContent` |
| Changed intent, changed actor, bounded payload | existing `ArtifactRetentionTests` cases |
| Bounded history | single immutable fact (revision fixed at 1) |

Malformed-history tests were not added, by user preference.

## Event contract status

`ArtifactRetentionBasisRecorded` carries source, revision, source position, policy, period, retains-through, basis kind, reason, recorder, time, operation provenance and response refs. No add, rename, remove or payload change requested. `RecordedBy` and `RecordedAt` duplicate `Operation.Actor` and `Operation.AcceptedAt` (enforced equal on apply); noted only, not changed because the event is baseline wire shape.

## Later-stage follow-ups

- Handler: `RecordArtifactRetentionBasisHandler` already exists; no aggregate-driven change.
- Reactor/projector: retention snapshot assembly and disposition eligibility read the basis; out of scope.

## Evidence

Tests written, not executed; build and run pending. RED not observed (no gap needing a fix). Status: done pending build.

## Review before build

Independent review, nothing compiled or run.

### Findings and fixes

1. **Test would throw (fixed).** `ShouldRefuseSecondDecisionGivenRecordedBasisAndDifferentOperation` recorded a contender operation (different logical id) on an owner selected for the first id. `NativeAcceptance.Decide` throws `ArgumentException` when the candidate logical id is not the selected one, so the test would have errored instead of asserting Conflict. Rewritten: history is replayed onto an owner selected for the contender id (as a handler hydrates it), then expected revision 0 and 1 are both refused with Conflict, no new facts, period end unchanged, original history still one fact.
2. Source reviewed: every accepted decision enforces RetainsThrough = end + 7y, kind/source match and exact-source binding; apply re-validates. Exact retry is Replayed with no event. No source change.
3. Other tests checked against rules (`ValidOperation`, `ValidPeriod` year <= 9992, `ValidSource` lowercase hex, `AddYears` leap clamp 2027-02-28); usings and `OperationProvenance` shape verified against the sibling `ArtifactRetentionTests`.

### Jev (model jev-1.14.0, advisory; request/response in `implementation-jev/artifactretentionbasis-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Second different operation refused Conflict, no event | refuse_conflict_no_event | 1.00 |
| Test asserts one basis per content and competing op appends nothing (Noul) | holds | 0.88 |
| RecordedBy/RecordedAt duplicating Operation.Actor/AcceptedAt | keep_baseline (remove 0.22) | 0.71, confidence 0.56 |

Disposition: first two confirm the code and test. The duplicate-metadata answer is a lead but low confidence; left unresolved, no event change. Catalog reconciliation decision.

### Remaining risks

- Not compiled or run.
- `Decide` throws for a mismatched logical id (framework behavior); handlers must hydrate by the operation's own logical id.
- RecordedBy/RecordedAt duplication unresolved (low confidence).


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
