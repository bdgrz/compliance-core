# PracticeIndependenceCommitment aggregate review, 2026-10-29

Scope is aggregate and domain events. Focused RED was captured. Handlers, reactors and the all-subject acceptance process remain separate.

## Identity, invariant and boundary

One tenant/(subject kind, subject ID) stream owns a permanent `advisory` or `attest` practice wall. A subject cannot be reserved or committed for the opposite practice, even after an engagement ends; this preserves M0-D26's lifetime person exclusivity. Staff and canonical-user subjects are distinct owners. Keep the subject's reservations, permanent wall and settlement fences together because the conflict decision must be atomic on that subject. The acceptance process acquires the complete staff/user reservation set in canonical stream order and proves irreversible authorization before asking these owners to commit. That cross-owner proof belongs to the receiver and process, not a projected roster.

| State | Decision | Outcome |
|---|---|---|
| No conflicting wall or hold | Reserve exact slot for declared practice/engagement | Temporary `PracticeIndependenceReserved` |
| Opposite active hold | Reserve | Transient conflict, no fact |
| Opposite permanent wall | Reserve | Permanent conflict, no fact |
| Matching exact active slot | Commit same practice and engagement | `PracticeIndependenceCommitted`; permanent wall |
| Missing or differing active slot | Commit | Conflict, no fact |
| Reserved or absent slot | Settle | Settlement/fence; permanent wall is not removed |

The owner matches a commitment's live reservation by root ID, first owner, generation, purpose, practice and engagement. `PracticeCommitmentHistory.ReadAsync` additionally requires the terminal operation to retain the reserved root actor, episode, original expected revision, authored intent digest, affected owner and expected-revision owner. The existing `Commit` method does not compare these fields; it can append a fact that the mandatory history verifier rejects. The normal receiver constructs the commit from the frozen authorized root, but direct owner behavior must not create a mismatched fact.

`PracticeIndependenceCommitmentOwnerTests.ShouldCommitOnlyMatchingRootGivenFinalizedReservation` creates a real selected reservation and attempts a valid new receiver operation or one with each changed root field. It asserts a matching root commits and a mismatched root conflicts without a commitment fact. The coordinator ran `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PolicyCampaignParticipantOwnerTests|FullyQualifiedName~PracticeIndependenceCommitmentOwnerTests|FullyQualifiedName~ProviderAssuranceGapOwnerTests'` before the fix: 53 failed and 8 passed across the combined selection. `Commit` now checks the same root fields as `PracticeCommitmentHistory.SameRoot` before raising a fact; receiver logical ID and causation remain free to differ. GREEN remains pending. Existing `PracticeIndependenceCommitmentTests`, `PracticeCommitmentNativeHistoryTests`, and `EngagementAcceptanceProcessTests` cover identity separation, permanent versus temporary walls, selected use, fences and coordination. They have not been rerun here.

Three selected events match source (`PracticeIndependenceReserved`, `PracticeIndependenceCommitted`, `PracticeIndependenceReservationSettled`). Their owner and operation envelope can repeat stream identity; independent Jev and compatibility review is required before any event change. Cross-owner authorization, real concurrent storage appends, recovery and hosted evidence are not established by the new owner test.

## Independent review before build

The new `SameRoot` compares exactly the fields compared by `PracticeCommitmentHistory.SameRoot`. The receiver derives reserve and commit provenance from the same frozen original operation, while logical IDs and causation events vary by stage. The reviewer changed the positive test fixture to use distinct intent references: the reserve fact holds a source-use authorization envelope, while the commit points to the parent operation intent. `CommitPracticeIndependenceHandler` explicitly verifies that the reservation envelope's `ParentIntent` matches the commit request; requiring the two references themselves to match would reject valid work. These fixture changes have not been compiled or run.

The reducer's commitment branch does not itself call `SameRoot`, but `PracticeCommitmentAggregateReader` performs the full history verification that does. This review does not establish correctness for callers bypassing that reader or for persisted state from malformed histories; the added guard prevents this direct append from creating a mismatched root going forward.

Jev `jev-1.14.0` (advisory; request and response in `implementation-jev/practiceindependencecommitment-test-impl-review-2026-10-29-*.json`):

| Question | Result | Disposition |
|---|---|---|
| Root fields bind commitment to reservation | Noul 0.87 | Source comparison confirms fields match the full history verifier |
| Distinct reservation envelope and parent references can represent the same operation | Noul 0.45 | Uncertain; handler code directly establishes the relationship |
| Require identical intent references | do not require 0.97 | Retain source-proof validation in the receiver |
| Owner field duplicates stream identity | Noul 0.78 | Confirmed by record and stream |
| Owner field disposition | keep shared envelope 0.51; source resolution 0.45 | Near-even; unresolved pending shared protocol and compatibility evidence |

No event schema change. GREEN and source-authority/process qualification remain pending.

## Coordinator validation (2026-10-29)

Release compilation passed with zero warnings and errors. The related participant, practice-commitment and provider-gap selection passed 124 tests; existing rollover selection passed 18 tests but does not test the missing execution claim. Changed C# files passed whitespace verification. The full non-broker, non-web suite passed 7,951 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The full suite is not green. Current producer paths were reviewed; persisted histories, hosted CI, and production behavior remain unqualified.
