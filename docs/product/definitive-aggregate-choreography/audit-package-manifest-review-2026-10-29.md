# AuditPackageManifest aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep (selected split already applied).** `AuditPackageManifest` (stream `audit-packages/{packageId}` under the tenant) owns manifest identity, immutable content-version references, local approvals (validation, override, disclosure) and build and delivery admissions as one conflict set. Command state holds only revision, current version, current and selected content refs and frozen flags; selections, archive bytes, build attempts and delivery children are outside (`AuditPackageBuildProcess`, `AuditPackageDelivery`).

- Identity: tenant + `packageId` (caller-supplied creation identity). The selected logical id, command kind and version feed `Acceptance` and selected-cut hydration only, never `EventStreamAddress`.
- Invariant: manifest identity, selected immutable content version and delivery gate. A delivery-admitted version is frozen; changes need an amendment creating the next version. Per-entry path/hash/size completeness is content-level (rules/archive builder), not owner state.
- Exclusions: build attempts, archive bytes, delivery children, worker attempts, read snapshots.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Create (own root operation) | v1, revision 1 | `AuditPackageCreated` |
| created | Amend (exact revision, frozen allowed) | next version | `AuditPackageAmended` |
| current, unfrozen | Request build / Validate / Override / Disclosure | next revision | `...BuildRequested`, `...Validated`, `...ValidationOverrideApproved`, `...DisclosureApproved` |
| build requested | Record build result (process, proof of exact cut) | next revision | `AuditPackageBuilt` |
| built and approved | Admit delivery (proof) | version frozen | `AuditPackageDeliveryAdmitted` |
| frozen | non-amend write | conflict | none |
| any | stale revision, wrong program, non-current version, foreign provenance, stale cut | conflict/validation/not found | none |
| any | exact retry | original result via `ReplayCut`/`Acceptance` | none |

## Changes

No production change. Source review found no real gap: every command validates `PrepareWrite`, ownership, root command kind and replay cut before `RaiseEvent`; `ApplyFact` pins tenant, subject, next revision, operation owner/episode, program binding, version monotonicity and frozen immutability.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveSameStreamGivenDifferentSelectedCommandsAndVersions` (new) |
| Invalid create appends nothing | `ShouldRefuseCreateGivenMissingProgramIdentity`, `ShouldRefuseCreateGivenExistingPackageIdentity` (new) |
| Stale revision, wrong program, non-current version | `ShouldRefuseWriteGivenStaleRevisionWrongProgramOrNonCurrentVersion` (new) |
| Frozen at delivery admission | `ShouldFreezeManifestAtAdmissionGivenDeliveryChildNotMaterialized` |
| Retry after later versions / historical handoff | `ShouldMaterializeHistoricalHandoffAndReplayGivenLaterManifestVersion`, `ShouldComposeCapturedHistoricalVersionGivenLaterManifestNativeFacts` |
| Competing/stale proof | `ShouldRefusePreparedBuildProofGivenLaterNativeManifestCut`, `ShouldRejectAnotherBuildRootGivenExactPublishedNativeAdmission` |
| Replay of valid history | `ShouldRequireExplicitSelectedFinalizedAuthorityGivenDirectPackageAppend`, handler tests |
| Bounded history | `ShouldRetainBoundedManifestAuthorityGivenManyActualNativeManifestVersions` |

Malformed-history tests were not added, by user preference.

## Event contract status

Facts carry tenant, subject, program, local revision, package version, content refs and operation provenance. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handlers/reactors: `AuditPackageBuildProcess` (T031/T032) and delivery materialization stay in their own stages.
- Projectors: package views and version lists are projections.

## Evidence

- Jev: no request this round.
- Tests: written, not executed; build and run pending. Intended: `dotnet test ... --filter "FullyQualifiedName~AuditPackageManifest"`.
- RED: none captured; the new tests cover behavior already present in source.
- Residual risk: blind-written tests assume `Fixture.SeedBuiltAndValidatedPackageAsync` stays internal-visible and `EventStreamAddress` has value equality.

## Review before build (independent, 2026-10-29)

Nothing was built or run.

### Findings

- Source re-read: every command runs `PrepareWrite`/`PrepareCreate`, ownership, root command kind and `ReplayCut.Require` before `RaiseEvent`; `ApplyFact` pins tenant, subject, next revision, version monotonicity and program binding. No implementation gap found; no payload field repeats stream identity.
- Tests re-read: the four new tests assert real outcomes (stream equality, Validation/Conflict/NotFound kinds, owner not created, exact current write accepted). Names follow CNTRYL0001, phases follow CNTRYL0002.
- Compile check: `Fixture` is `internal sealed` nested in a public class (same assembly, visible); `Fixture()`, `TenantId`, `ProgramId`, `Reader` and `SeedBuiltAndValidatedPackageAsync` exist; `HydrateAsync(new AuditPackageManifest(...))` is used the same way in sibling tests; `Assert.Equal` on `Stream` is used in sibling tests (value equality confirmed by precedent). `RequestErrorKind` via `Cntryl.Portia`, `Uuid` same. No usings missing found.
- Known coverage limit: the frozen-version refusal at aggregate level is covered by existing delivery tests, not by these four.
- Fixes applied this review: none required.

### Jev (model jev-1.14.0, advisory)

Request/response: `implementation-jev/auditpackagemanifest-test-impl-review-2026-10-29-{request,response}.json`.

| Question | Answer | Probability |
|---|---|---|
| Where to enforce entry completeness (path/hash/size) | content_layer_ok | 1.0 |
| Premise: owner not inspecting entries still enforces the delivery gate | true | 0.87 |
| Premise: new tests assert guard behavior | true | 0.87 |

Discrepancy resolved: the catalog's entry-completeness refusal is enforced at the content/rules layer plus exact-cut proof at the owner; no owner change, no event change.

### Remaining risks

- Tests unexecuted; build and run pending.


## Coordinator build and test evidence (2026-10-29, wave 7)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7645 passed, 5 failed of 7650.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
