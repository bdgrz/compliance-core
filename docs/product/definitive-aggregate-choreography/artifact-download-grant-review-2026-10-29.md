# ArtifactDownloadGrant aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together.** One receiving grant (`ArtifactDownloadGrant`, stream `artifact-download-grants/{grantId}`) decides atomically that a protected artifact download command ends in exactly one terminal outcome: an issued ticket/result or a cancellation fence.

- Identity: UUIDv5 in the artifact namespace over `artifact-download-grant:v1:{tenant}:{commandId}`. The stream address derives from tenant and that id only; no other caller key forks a stream.
- Invariant: one terminal outcome per grant. Issue and cancel are mutually exclusive; the original ticket never renews under the same root identity.
- Exclusions: the download request, source-use acquisition and settlement, ticket minting/encryption, and authorization proofs belong to EvidenceArtifact and the `ProtectedArtifactDownloadGrantReactor`/handler (later stages).

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Issue (valid) | issued | `EvidenceArtifactDownloadIssuedV2` |
| none | Issue (invalid) | validation refusal, no event | none |
| none | Cancel (valid) | canceled | `ArtifactDownloadGrantCanceled` |
| none | Cancel (invalid) | validation refusal, no event | none |
| issued | Issue, exact retry | success, original ticket, no event | none |
| issued | Issue, changed intent | conflict, no event | none |
| issued | Cancel | no-op success, no event | none |
| canceled | Cancel, exact retry | success, no event | none |
| canceled | Cancel, changed | conflict, no event | none |
| canceled | Issue | conflict, no event | none |

## Changes

- Gap fixed: the catalog (choreography A021) says only that cancellation is guarded by "Grant not already issued" and that a repeated cancellation is a "retry no-op"; it does not state the result of an abort on an issued grant. (The quoted phrase "an abort after a terminal outcome is a no-op" is not catalog text; see Review before build.) `Cancel` after issue returned a conflict. It now returns success with no event; the grant stays issued. The prior test that asserted refusal was updated to the no-op.
- Coverage added: invalid issue/cancel append nothing; exact cancel retry and changed generation; valid replay of an issued history (retry, abort no-op) and of a canceled history (issue fenced).
- No other defect found. Apply guards re-validate each fact and reject a second terminal fact.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveGrantIdentityGivenArtifactTenantAndCommand` (existing) |
| One terminal outcome, issue fenced | `ShouldFenceIssueAfterCancellationGivenOneTerminalGrantOutcome` (existing) |
| Abort after issue is a no-op | `ShouldTreatCancellationAsNoOpGivenIssuedGrantTerminalOutcome` (updated) |
| Exact retry vs changed intent (issue) | `ShouldReplayOriginalTicketGivenExactIssueRetryAndRefuseChangedIntent` (existing) |
| Exact retry vs changed (cancel) | `ShouldReplayCancellationGivenExactRetryAndRefuseChangedGeneration` (new) |
| Invalid decisions append nothing | `ShouldAppendNothingGivenInvalidIssueOrCancellation` (new) |
| Valid replay, retry after history | `ShouldRestoreIssuedTicketAndAcceptExactRetryGivenValidHistoryReplay`, `ShouldRestoreCancellationAndFenceIssueGivenValidHistoryReplay` (new) |
| Competing decisions | issue vs cancel tests above; the single terminal slot decides |

Malformed-history tests were not added, by user preference.

## Event contract status

- `EvidenceArtifactDownloadIssuedV2` and `ArtifactDownloadGrantCanceled` replay. No payload, rename or discriminator change is requested.
- Observation, not a request: `Operation` and `Intent` carry provenance beyond business facts; the shared native-operation envelope owns that and it is left as is.

## Later-stage follow-ups (not aggregate gaps)

- `ProtectedArtifactDownloadGrantReactor`/`IssueProtectedArtifactDownloadGrantHandler`: ordered source-use acquisition, DecisionAt validity checks, ticket encryption, current original-actor scoped read.
- Cancel handler/abort wiring and terminal proof settlement; request and read flow.
- Read-only v1 decoder compatibility stays with the decoder.

## Evidence

- Jev: no new request this round. The boundary rests on the catalog and code.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ArtifactDownloadGrantTests"`, then the unit suite.
- RED: none captured; not claimed.
- No host, durable-store or production qualification is claimed.

## Review before build (independent review, 2026-10-29)

Findings
- Misattributed catalog quote: the earlier note quoted catalog text that does not exist. A021 guards cancel with "Grant not already issued" and calls only a repeated cancel a "retry no-op". Cancel-after-issue result is a catalog gap. Fixed in the text above.
- Risk fixed: exact-retry detection used record `==` between a stored (metadata-attached after replay or reload) fact and a fresh candidate. If `DomainEvent` equality includes committed metadata, every retry after reload would wrongly Conflict, and the new replay tests would fail. Replaced with payload-only `SamePayload` comparers (business fields only).
- Tests reviewed: each asserts outcome (error kind, pending event count, state after replay); names and phases conform; fixtures (64-char lowercase hex, member actor with GUID id, operation owner = grant stream, root = command id) satisfy ValidateIssued/ValidateCanceled. No test weakened.

Jev (model jev-1.14.0), request/response in implementation-jev/artifactdownloadgrant-test-impl-review-2026-10-29-*.json
- Cancel after issue: success_no_event 0.68, needs_source_resolution 0.27, conflict_refusal 0.05 (choice confidence 0.52). Premise "catalog silent on this" 0.87. Lead is clear over conflict but not decisive; kept no-op in code, flagged as a catalog gap to register (eventChangesNeeded none; catalog wording only).
- Issued payload repeats TenantId/ArtifactId/GrantId: keep_as_cataloged 0.94. Applied (no change).
- Fence test asserts invariant: 0.80 true. Test only checks the Conflict kind loosely via IsSuccess false; acceptable.

Remaining risks
- Not built or run. Unconfirmed: `DomainEventSeed.Attach(fact, id, 1)` 3-arg overload (used identically in other test files); DomainEvent equality semantics (now bypassed).
- Catalog should state the abort-on-issued result explicitly (shared file, not edited).


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
