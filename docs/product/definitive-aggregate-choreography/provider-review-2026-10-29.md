# Provider aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together; name uniqueness stays split to `ProviderNameClaim`.** One tenant-scoped provider (`Provider`, stream `providers/{providerId}`, tenant realm) decides, atomically, its own content/revision lifecycle: at most one pending change, a generation fence for delayed apply or cancel, and revisions that advance only when a change is applied. The stream keeps content references (last two revisions) rather than content, so replay does not grow with content history.

- Identity: tenant plus caller `ProviderId` (immutable creation identity). Later changes bind `CommandId` to this identity and the expected revision. The stream address derives from tenant and `ProviderId` only, so no request key can fork a stream.
- Invariant: one provider record; one pending change; revision equals the number of applied changes; a stale expected revision, a second creation, or a revise of an unrecorded provider is refused.
- Exclusions: governed-name uniqueness belongs to `ProviderNameClaim` and the provider name-change process; materiality basis (customer_data / critical_path, rationale, spend never a basis) is content validation in `ProviderRules` before a change is accepted; assurance gaps, reports, reviews and risk acceptances are separate owners.
- Split justification: a tenant-wide name-uniqueness rule cannot live on one provider stream without serializing all providers; the claim owner holds it and the process orders claims (canonical order) before apply.

## Lifecycle

| State | Command (via `NamedRecord`) | Result | Event |
|---|---|---|---|
| none | AcceptChange create | pending, generation 1 | `ProviderChangeAccepted` |
| none | AcceptChange revise | not found, no event | none |
| pending | AcceptChange (any) | transient conflict, no event | none |
| pending | ApplyChange, matching root and intent, revision 0 | revision 1 | `ProviderRecorded` |
| pending | ApplyChange, matching root and intent, revision n | revision n + 1 | `ProviderRevised` |
| pending | ApplyChange, other root or intent | conflict, no event | none |
| pending | CancelChange, matching root | fence, nothing applied | `ProviderChangeCanceled` |
| none pending | Apply / Cancel | conflict (no-op for late delivery), no event | none |
| recorded | AcceptChange create | conflict, no event | none |
| recorded | AcceptChange revise, stale revision | conflict with current revision, no event | none |
| recorded | AcceptChange revise, expected = revision | pending, generation + 1 | `ProviderChangeAccepted` |

Retirement is a planned distinct terminal fact (`ProviderRetired`, decision EV-D07, selected for target definition, Jev 0.75). It is not implemented here and not invented; see event changes.

## Changes

- **Real gap fixed (source).** `Provider` held its tenant but its reducers (`ProviderRecorded`, `ProviderRevised`) never compared the applied fact's `TenantId` with the tenant the owner was opened for; stream tenancy alone does not validate payload identity. `Location` already had this guard. `Provider` now routes both applied facts through `ObserveProviderApplied`, which refuses a fact for another tenant before the shared `ObserveApplied` checks (record id, revision, pending root/intent). Valid history is unaffected because `ApplyChange` raises with the owner's own tenant.
- No test for the refusal itself: it is a malformed-history case, which the user has asked not to add. Valid replay with tenant-carrying facts is covered.
- Coverage gap closed: no test exercised `Provider` as an aggregate. Existing coverage went only through the process host (`NamedRecordProcessTests`) and HTTP tests. New `ProviderAggregateTests.cs` covers the criteria below directly.

## Test-to-criterion table

| Criterion | Test (`ProviderAggregateTests` unless noted) |
|---|---|
| Accept holds a pending change, generation 1 | `ShouldHoldPendingChangeGivenAcceptedCreation` |
| Applied creation is `ProviderRecorded`, later is `ProviderRevised` with revision and tenant | `ShouldRaiseRecordedThenRevisedGivenTwoAppliedChanges` |
| One pending change; invalid decision appends nothing | `ShouldRefuseSecondChangeAndAppendNothingGivenPendingChange` |
| Revise of unrecorded provider refused | `ShouldRefuseRevisionAndAppendNothingGivenUnrecordedProvider` |
| Second creation refused | `ShouldRefuseCreationAndAppendNothingGivenExistingProvider` |
| Stale revision / competing decision | `ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedProvider`; process level `ShouldRefuseStaleExpectedRevisionAndAppendNothingGivenAdvancedProvider` (existing) |
| Foreign stream operation refused | `ShouldRefuseOperationOfAnotherOwnerAndAppendNothingGivenForeignStream` |
| Apply with nothing pending is a no-op refusal | `ShouldRefuseApplyAndAppendNothingGivenNoPendingChange` |
| Delayed apply fenced after cancel; next generation allowed | `ShouldFenceLateApplyAndAllowNextGenerationGivenCanceledRevision` |
| Canceled creation leaves no record | `ShouldRefuseCreationAndAppendNothingGivenCanceledCreation` |
| Cancel after applied change is a no-op refusal | `ShouldRefuseCancelAndAppendNothingGivenAppliedChange` |
| Bounded history (last two revisions kept) | `ShouldKeepOnlyLastTwoRevisionsGivenThreeAppliedRevisions` |
| Valid replay restores revision, generation, pending, tenant-carrying facts | `ShouldRestoreRevisionAndPendingChangeGivenValidHistoryReplay` |
| Exact retry after later facts returns the original acceptance; changed intent conflicts | `ShouldReturnOriginalAcceptanceGivenExactRetryAfterLaterFactsAndConflictGivenChangedIntent`; process level `ShouldReplayOriginalResponseGivenExactRetryAfterLaterChangeAndConflictOnChangedIntent` (existing) |
| Rename and claim ordering, duplicate name, independence of other providers | `NamedRecordProcessTests` (existing) |
| Materiality basis and content validation | `ProviderRules` tests (existing, content-level; not re-reviewed here) |

## Event contract status

- Facts `ProviderChangeAccepted`, `ProviderChangeCanceled`, `ProviderRecorded`, `ProviderRevised`. Payloads carry owner address, revision, generation, intent and content references, tenant/provider ids and the applied actor/time; operation provenance is the native envelope shared by all named records.
- No rename or payload change is requested for existing facts.
- Planned (catalog EV-D07): `ProviderRetired` as a distinct terminal fact. Not added; it needs a source-backed retirement command and a name-release interaction with `ProviderNameClaim` first.

## Later-stage follow-ups (not aggregate gaps)

- `ProviderRetired` event, retire command and handler, and the claim release on retirement (reactor/process stage).
- Provider directory and paging projections stay projections of this stream.
- Handler wiring of `ApplyProviderChange` and cancellation recovery stays with the name-change process.

## Evidence

- Jev: no new request this round. The retirement decision rests on EV-D07 (Noul 0.75, jev-1.14.0), recorded in `decisions.json`. Boundary rests on the catalog and code, not on a score.
- Tests: written, not executed; build and run pending. Intended commands: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ProviderAggregateTests|FullyQualifiedName~NamedRecordProcessTests"`, then the backend unit suite.
- RED: none captured. The tests cover behavior already implemented; the tenant guard is a reducer hardening with no valid-history failing case, so no red is claimed.
- No host, durable-store or production qualification is claimed.

## Review before build

Independent review before any build. Evidence is written, not executed.

### Findings and fixes

- Implementation: `Provider` tenant guard in `ObserveProviderApplied` mirrors `Location`; `INamedRecordAppliedFact.RecordTenantId` exists and is implemented by both facts. No duplicate-fact path found: accept/apply/cancel all check pending, root, intent and owner before raising, so stale or competing inputs append nothing.
- Test gap fixed: no test showed that an apply with a different intent, or a cancel with a different root, is refused while the pending change is kept. Added `ShouldRefuseApplyAndKeepPendingGivenDifferentIntent` and `ShouldRefuseCancelAndKeepPendingGivenDifferentRoot`.
- Behavior undocumented by tests: a canceled creation burns the caller-supplied `ProviderId` (a second creation is refused with Conflict). Pinned with `ShouldRefuseRepeatedCreationAndAppendNothingGivenCanceledCreation` after Jev agreed.
- Compile read: usings match `LocationAggregateTests` plus `NativeOperations.Services`, `Providers.Aggregates` and `Providers.Events`; `OperationProvenance` constructor arity, `AcceptedOperationBinding.Of`, `NativeAcceptanceDecision` and `NativeFacts.Raised/Replay` match their definitions. Test names and Arrange/Act/Assert phases follow CNTRYL0001/0002. Could not confirm: the exact member names Assert.Collection-style analyzers accept, and unused-using diagnostics (the using list follows the sibling file, so the same behavior is expected).

### Jev (jev-1.14.0, advisory), request and response in `implementation-jev/provider-test-impl-review-2026-10-29-*.json`

| Question | Answer |
|---|---|
| Apply with different intent test asserts mismatched apply cannot append a wrong fact | Noul 0.90 |
| Late apply after cancel then next-generation accept asserts the fence | Noul 0.80 |
| Canceled creation burns the id (second creation refused) | Choice keep_refuse 0.96, allow_retry 0.02, needs_source_resolution 0.02 |
| Replay tenant guard consistent with one-tenant-per-stream invariant | Noul 0.89 |

### Discrepancies

- Canceled creation id reuse: decisive keep_refuse (0.96); applied by pinning the behavior in a test. No code change.
- `ProviderRetired` (EV-D07) is in the catalog as planned and absent from code: already a recorded catalog decision, not a new discrepancy; stays a later-stage event addition.

### Remaining risks

- `ProviderRetired` shape and name-claim release are not source-backed.
- Tenant-guard refusal has no direct test (malformed history, per user preference).
- Nothing built or run.


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
