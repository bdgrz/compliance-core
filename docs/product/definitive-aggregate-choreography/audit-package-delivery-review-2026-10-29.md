# AuditPackageDelivery aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep as the catalog's independent child owner (no split, no merge).** One delivery stream, `audit-package-deliveries/{identity}` under the tenant, decides one invariant: a personal external handoff is materialized exactly once, as the historical record of one exact admission on the manifest, with its immutable original terminal result.

- Identity: `AggregateIdentity(AuditPackageDelivery, tenant, packageId, manifestVersion, deliveryId)`. No caller key outside those four values forks the stream. The selected logical id only selects the acceptance in a hydration; it is not part of the address.
- Invariant: the handoff exists at most once; it is materialized only if the intent, operation and admission reference all name this package, version and delivery, the manifest as first, affected and revision owner, the admission fact as cause, and the original actor and authored-intent binding.
- Exclusions (owned elsewhere): the delivery gate, disclosure approval, validation and "no prior delivery for the exact recipient/version" are decided by `AuditPackageManifest` at `AuditPackageDeliveryAdmitted`; reading and verifying the frozen intent and proofs, appending the receiver result and the original terminal response belong to the handler and verifier; the manifest's delivery projection is a view.
- No split: the child has a single fact and no lifecycle beyond it.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Materialize (exact admission, intent and operation) | materialized, revision 1 | `AuditPackageDelivered` |
| none | Materialize with any differing package, version, delivery, root, owner, admission stream/version/event, command kind, causation, actor, episode, intent hash, or empty program or logical id | conflict | none |
| materialized | Materialize again (exact retry or changed intent) | conflict | none |

Exact retry versus changed intent after materialization is decided by `Acceptance.Decide` (full `AcceptedOperationBinding`), which the handler consults before calling the aggregate.

## Changes

- Source: none. The command validates before `RaiseEvent` and its checks cover everything the replay guard (`On<AuditPackageDelivered>`) rejects that a caller can control, so no unreplayable fact can be appended. No real gap found in identity, invariant or lifecycle.
- Tests: new `AuditPackageDeliveryTests.cs`; existing `AuditPackageNativeDeliveryTests` (handler and history level) stay as they are.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no key forks stream | `ShouldDeriveStableIdentityGivenTenantPackageVersionAndDelivery` |
| Valid materialization, payload carries business facts | `ShouldMaterializeHandoffAtRevisionOneGivenExactAdmission` |
| Invalid decisions append no events | `ShouldRefuseAndAppendNothingGivenHandoffThatDiffersFromAdmission` (16 cases) |
| No-op / exact retry / changed intent after fact | `ShouldRefuseSecondMaterializationGivenHandoffAlreadyMaterialized`, `ShouldRetainHandoffAndDecideReplayGivenReplayedHistory` |
| Replay / hydration of valid history, acceptance decision | `ShouldRetainHandoffAndDecideReplayGivenReplayedHistory`, `ShouldDecideNoneGivenUnrelatedLogicalIdOnReplayedHistory` |
| Competing appends / stale revision | single-fact stream: a second materialization is a conflict (above); the executor's expected-version append settles a true race (existing native history suite) |
| Bounded history | one event per stream by construction |
| Later manifest version, terminal result, archive unavailable, proof read failures | existing `AuditPackageNativeDeliveryTests` |

Malformed-history tests were not added, by user preference.

## Event contract status

- `AuditPackageDelivered` carries business facts (recorder, frozen intent, recorded time, program, package, version, delivery, admission reference) plus operation provenance and terminal result. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handler and `AuditPackageDeliveryAdmissionReactor` already exist; their wiring is outside this stage.
- Delivery list projection on the manifest view.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and source replay guard.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AuditPackageDelivery"`.
- RED: none captured; not claimed. No code change was needed, so these tests are verification coverage.
- Residual risk: blind-written test compile errors (for example the `EventStreamAddress.Area` property and record `with` on `NativeFactReference`).

## Review before build

Independent review of source and tests; written, not executed.

### Findings and fixes
- Test: `version.ToString()` in the `Valid` helper would trip CA1305 under strict analyzers. Fixed with `CultureInfo.InvariantCulture` and `using System.Globalization`.
- Test coverage: the refusal theory lacked cases for header affected and revision owners and for operation root id, first owner, affected owner, revision owner and original expected revision, all of which the guard checks. Added 7 cases (23 total).
- Test: the materialize test did not assert the tenant, the operation provenance or the terminal result on the fact. Added.
- Source: no defect found; the command checks cover the replay guard's caller-controlled conditions, so no unreplayable fact can be appended. Noted, not changed: `intent.RecorderMemberId` is not compared with `operation.Actor` (the actor is bound to the header's original actor); the catalog does not require it.

### Jev (jev-1.14.0), files in `implementation-jev/auditpackagedelivery-test-impl-review-2026-10-29-*.json`
- Second Materialize returns Conflict, exact retry acknowledged by `NativeAcceptance.Decide`: consistent, 1.00.
- Tests assert the invariant: noul 0.66 (moderate; the 7 added cases address the gaps found, so this stays advisory).
- Payload restates stream identity (TenantId, SubjectId, Revision, PackageId, PackageVersion, DeliveryId): report_as_catalog_followup 0.58, keep_no_action 0.41, remove_now 0.01. Not decisive; unresolved. No event change applied; flagged as a shared-file follow-up only.

### Remaining risks
- Not compiled. Unconfirmed by execution: `EventStreamAddress` record equality in the identity test, `AggregateScenario` pending count after replay.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
