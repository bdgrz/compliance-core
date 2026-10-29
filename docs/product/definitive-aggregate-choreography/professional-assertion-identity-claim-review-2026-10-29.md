# ProfessionalAssertionIdentityClaim aggregate review, 2026-10-29

Scope: aggregate and domain-event stage. Tests are written, not executed; build and test are pending.

## Boundary and lifecycle

The platform-scoped stream is keyed by assertion ID. This selected split permanently binds that assertion identity to the frozen verified change intent, so two competing changes cannot claim it. Keep the claim as its own owner: the designation owns authority revision and staff proof, while the claim serializes identity uniqueness across designations. There is no release lifecycle. The process verifies the signature, frozen tuple, source and staff-use proof before admission; these checks and target progression are later-stage responsibilities. The immutable intent contains the digest, designation, subject, duty and scope; the owner does not inspect its content.

| State | Decision | Result |
|---|---|---|
| Unclaimed | Claim on this assertion with a nonempty logical ID | Append `ProfessionalAssertionIdentityClaimed` at revision 1 |
| Any | Empty assertion or logical ID, foreign owner or root assertion | Validation refusal; no fact |
| Claimed | Claim | Permanent conflict; no fact |
| Claimed, selected operation | Compare exact accepted binding | Replayed; no fact; changed binding conflicts |

A stream contains one fact at most, so its history is bounded. Concurrent claims share the assertion stream and require framework OCC; after a conflict the receiver rehydrates and re-decides. A replayed fact restores both the permanent claim and the selected-operation acceptance. The receiver handles the exact retry using `NativeAcceptance`; direct `Claim` on an already-claimed owner refuses a second fact.

## Changes and coverage

The claim now rejects empty assertion and logical IDs before append. The receiver already rejects both, but the owner could otherwise persist a claim that has no assertion identity or selectable acceptance ID. Direct tests cover fresh claim, foreign stream and assertion refusal, the two empty identifiers, valid replay, exact versus changed operation binding, and a competing claim with no new fact. Tests are `ProfessionalAssertionIdentityClaimTests` and were written before the fix; RED was not observed. Existing `ProfessionalDutyChangeTests` exercise receiver and process behavior, including competing claims and proof validation; those tests do not replace owner-level or durable-store qualification.

## Event contract and follow-ups

The selected final event catalog contains `ProfessionalAssertionIdentityClaimed`, matching the implementation. The event includes the owner, revision, generation, immutable intent and native-operation envelope; whether duplicated stream identity and envelope fields warrant a versioned event change is for independent Jev review and coordinator disposition. No event change was made. The older preliminary list also mentions `ProfessionalAssertionIdentityClaimReleased`, while the selected final event list and invariant specify a permanent claim; the reviewer should resolve that historical discrepancy against source with Jev. Handler, reactor, signature and staff-proof work is out of scope.

## Evidence and limits

No build or test was run in this implement phase. Intended focused selection: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ProfessionalAssertionIdentityClaimTests'`. RED was not observed; the independent reviewer must assess the new guards and tests. Actual serialized replay, persisted OCC, recovery and end-to-end behavior remain separate qualification boundaries.

## Independent review before build

Reviewed the current source, new tests, final catalog and receiver. The added guards match required assertion and receiver command identities; foreign-owner, mismatched root and already-claimed paths return before `RaiseEvent`. Exact retry and changed binding are decided by the receiver's selected `NativeAcceptance` before its call to `Claim`. Added `ShouldKeepStreamIdentityStableGivenSelectedOperationChanges` to check that selecting an operation cannot fork the claim stream. The replay test exercises a serialized clone via `NativeFacts.Raised` and `NativeFacts.Replay`, but has not been executed. A competing durable append is not directly tested here; framework OCC and receiver redecision must be qualified separately. The new code and tests are uncompiled.

Jev `jev-1.14.0` (advisory; exact request and response in `implementation-jev/professionalassertionidentityclaim-test-impl-review-2026-10-29-*.json`):

| Question | Result | Disposition |
|---|---|---|
| Preliminary release conflicts with selected permanent invariant | Noul 0.84 | Source invariant and selected final event list are authoritative |
| Release discrepancy | retain permanent claim 0.99 | No release event; preliminary entry is historical |
| Owner repeats stream identity despite native contract | Noul 0.87 | Duplication established |
| Owner field disposition | keep shared envelope 0.98 | Retain shared `INativeOperationFact` envelope; no event change requested |
| Tests check invalid/competing refusal without fact | Noul 0.92 | Advisory signal; assertions independently inspected |

No additional source finding or event change. The test fixtures and `NativeAcceptance` integration are checked by reading, not execution. This review does not establish persisted OCC, host, or production behavior.

## Coordinator validation (2026-10-29)

The Release test-project build passed with zero warnings and errors. After the provenance fixes, the four-owner focused selection passed 57 tests; the related staff and engagement selection passed 118 tests. The full non-broker, non-web suite passed 7,798 tests and failed five: the two ArtifactDownloadGrant coverage-map checks and three weekly-schedule original-result checks. Those same five failed on hosted develop before wave 2; the suite is not green. Independent source review found the normal operation builders satisfy the new guards. No durable-store, hosted CI, or production qualification is implied.

Whitespace verification passed for the four owners’ changed C# files using `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore --include <files>`.
