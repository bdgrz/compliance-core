# ArtifactDisclosure aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (catalog: retain).** One disclosure (`ArtifactDisclosure`, stream `artifact-disclosures/{disclosureId}`) binds immutable artifact content, handling-policy revision, recipient and purpose; delivery requires that approved identity and revocation is final.

- Identity: tenant plus `DisclosureId` only; the stream address derives from those, no caller key forks a stream. Selected mutations carry `CommandId` as logical identity; the aggregate resolves retries by comparing immutable intent (artifact identity, recipient, purpose, rationale, actor, timestamp / receipt), and `ExpectedRevision` is a concurrency precondition, not identity.
- Invariant (D16-13): disclosure is admitted only for an available artifact (quarantined, uninspected, rejected or disposed artifacts are refused), and every disclosure records recipient, purpose and handling-policy revision.
- Why together: separating approval from delivery without a one-use approval authorization could deliver changed content or a different recipient.
- Exclusions: source-snapshot reading, authorization, download grants, redacted-derivative production (a derivative is its own evidence artifact), receipt capture and notification belong to the readers, handlers and EvidenceArtifact.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Approve (valid, available source) | approved, rev 1 | `ArtifactDisclosureApproved` |
| none | Approve (invalid / unavailable source) | validation / conflict, no event | none |
| none | Deliver / Revoke | not found, no event | none |
| any | Approve, exact retry (even after delivery, revoke or source drift) | success, no event | none |
| any | Approve, changed intent | conflict, no event | none |
| approved | Deliver (expected rev, source unchanged) | delivered, rev 2 | `ArtifactDisclosureDelivered` |
| approved | Deliver, stale rev / changed content or policy / unavailable | conflict, no event | none |
| delivered / revoked-after-delivery | Deliver, exact retry | success, original receipt, no event | none |
| delivered | Deliver, changed receipt | conflict, no event | none |
| approved, delivered | Revoke (expected rev) | revoked, rev +1 | `ArtifactDisclosureRevoked` |
| revoked | Revoke, exact retry (any earlier expected rev) | success, no event | none |
| revoked | Revoke, changed | conflict, no event | none |
| revoked, never delivered | Deliver | conflict, no event | none |

Downloaded copies are never recallable; revoke preserves the delivery receipt.

## Changes

- Gap fixed (retry of a committed approval after later facts): `Approve` required `Status == "approved"` and re-validated the fresh source (availability, handling policy) before the retry check. An exact retry after delivery or revocation, or after the artifact was quarantined or its policy changed, returned a conflict or a source error instead of the original result. Selected replay rule: exact authorized retries resolve the original accepted result; fresh mutable feasibility cannot replace it. Retry now compares immutable intent only (artifact `Source` identity and digest, recipient, purpose, rationale, actor, timestamp) before any source validation.
- Gap fixed (delivery retry): the retry path used `SameSource`, so a retry after a handling-policy or position change conflicted, and a null snapshot threw a NullReferenceException. It now compares artifact identity only and refuses a null snapshot with a conflict.
- Fresh (first-time) approval and delivery keep full source validation, so the available-state invariant is unchanged.
- No other defect found. Apply guards re-validate each event (tenant, id, revision, transition, actor, source-position monotonicity, no recallable copies).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Admission: available, classified artifact | `ShouldRequireAvailableClassifiedArtifactGivenApproval` (existing) |
| Policy fence on delivery | `ShouldFenceDeliveryToApprovedPolicyVersionGivenPolicyChange` (existing) |
| Revoke preserves delivery; stale revoke; replay | `ShouldPreserveDeliveryAndRecordNonrecallableRevocationGivenRevoke` (existing) |
| Exact approval retry vs changed intent | `ShouldAllowOnlyExactApprovalRetryGivenDisclosureIdentity` (existing) |
| Approval retry after later facts and source drift | `ShouldReturnOriginalApprovalGivenExactRetryAfterDeliveryAndSourceDrift` (new) |
| Invalid decisions and missing stream append nothing | `ShouldAppendNothingGivenInvalidApprovalOrMissingDisclosure` (new) |
| Stale revision, invalid receipt, unavailable source on delivery | `ShouldRefuseDeliveryAndAppendNothingGivenStaleRevisionOrInvalidReceipt` (new) |
| Delivery retry after revoke; changed receipt; null source | `ShouldReturnOriginalReceiptGivenExactDeliveryRetryAfterRevocationAndRefuseChangedReceipt` (new) |
| Competing decisions: revoke fences delivery, revocation final | `ShouldRefuseDeliveryGivenRevokedUndeliveredDisclosure` (new) |
| Revoke retry vs changed decision | `ShouldAcceptExactRevokeRetryAndRefuseChangedDecisionGivenRevokedDisclosure` (new) |
| Valid replay and retries after history | `ShouldRestoreStateAndAcceptRetriesGivenValidHistoryReplay` (new) |
| Bounded history | At most three facts by construction (approve, deliver, revoke); `ValidateState` enforces revision 1..3 |

Malformed-history tests were not added, by user preference.

## Event contract status

- `ArtifactDisclosureApproved`, `ArtifactDisclosureDelivered`, `ArtifactDisclosureRevoked` replay. No add, rename, remove or payload change is requested.
- Observation, not a request: events carry the full `ArtifactDisclosureView` state snapshot plus `TenantId`/`SubjectId`/`Revision`/`ActorMemberId`/`OccurredAt`. The catalog marks the event authority as "retained baseline business facts, requires schema reconciliation"; that reconciliation is a shared-file item and is left as is.
- Catalog gap to register (wording only): the catalog lists `ArtifactRevision` on the approve command and `ExpectedRevision` on approve; the aggregate neither stores the artifact revision (the source snapshot position pins content) nor takes an approve expected revision (creation is guarded by stream absence). Handler-stage mapping decides; no aggregate change.

## Later-stage follow-ups (not aggregate gaps)

- Handlers: map `CommandId`/`ExpectedRevision`/`ArtifactRevision` to the aggregate calls; compute the fresh source snapshot; return the original result on exact retry.
- Approved redacted-derivative admission (D16-13) is the reader/handler choosing the derivative artifact as the source.
- Disclosure logging, notification and read model projections.

## Evidence

- Jev: not consulted this round. No near-even design question remained; the retry rule follows the catalog's selected replay rule.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ArtifactDisclosureTests"`, then the unit suite.
- RED: none captured; not claimed.
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review of the source, tests and this file (written, not executed; nothing built or run).

### Findings

- No correctness gap found. Walked every new test against the code: approve/deliver/revoke validation order, retry paths (approve compares immutable intent before source checks; deliver retry compares artifact identity, receipt, actor and time; revoke retry compares rationale, actor and time with `expectedRevision <= Revision`), stale revision, revoked-undelivered delivery fence and null-source delivery all produce the asserted kinds and event counts.
- Competing/duplicate input cannot append a wrong or duplicate fact: every retry returns success without `RaiseEvent`; stale and changed intents return conflict before any event. Event payload repeats tenant and disclosure identity (see D2) but no metadata field is duplicated beyond that.
- Tests assert behavior (result kinds, status, pending counts, replayed state), follow CNTRYL0001/0002, use `PendingEvents.Count`, `Assert.Single`, do not pass pending events to `DomainEventSeed.Attach`, and capture state before further commands in the replay test. Fixture values are valid (member actors with Guid ids, 64-hex digest, `evidence_artifact` kind, tenant matches).
- No edits were required.

### Jev (model jev-1.14.0, advisory; request/response in `implementation-jev/artifactdisclosure-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Exact approve retry after later facts or source drift | success_no_event | 1.00 (confidence 1.0) |
| Premise: catalog says retries resolve the original result | true | 0.92 |
| Revoked-undelivered delivery fence test proves revocation is final | true | 0.70 |
| D1/D2 Repeated identity in payload | keep_as_cataloged | 0.97 (confidence 0.95) |
| D3 ArtifactRevision/ExpectedRevision on approve | handler_stage_mapping | 0.72 (confidence 0.58) |

Dispositions:

- D1 (approve retry behavior): decisive, matches code; no change.
- D2 (payload snapshot repeats `TenantId`/`DisclosureId`): decisive keep. Event schema reconciliation stays a shared-file item; no event change requested.
- D3 (catalog lists `ArtifactRevision`/`ExpectedRevision` on approve, aggregate takes neither): clear but not strong lead (0.72, 0.27 needs_source_resolution). Handler-stage mapping adopted for now; the catalog wording gap stays flagged for the catalog owner (shared file, not edited).
- Revoke fence test: 0.70 is a moderate signal only; manual reading confirms it asserts conflict, status revoked, no delivery and two pending events, so it cannot pass if delivery were accepted.

### Remaining risks

- Not compiled or run. Confirm: `NativeFacts.Raised(seed)` JSON round-trip of `ArtifactDisclosureView` (needs the events registered in the JSON context), and that a freshly replayed aggregate reports zero pending events.
- Records equality on `ActorReference`, `ArtifactRetentionSource` and `ArtifactDisclosureDeliveryView` is assumed (existing code already relies on it).


## Coordinator build and test evidence (2026-10-29, wave 6)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7557 passed, 5 failed of 7562.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
