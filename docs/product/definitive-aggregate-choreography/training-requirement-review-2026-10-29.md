# TrainingRequirement aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (selected split target of TrainingCatalog, retained as one owner).** One requirement (`TrainingRequirement`, stream `training-requirements/{requirementId}` under the tenant) decides atomically a normalized identifier's creation and its sequential immutable content versions.

- Identity: the requirement id is the deterministic UUIDv5 over tenant, program and normalized identifier (`TrainingRequirementIdentity.IdFor`), so no caller key forks a stream. The identifier and program are immutable after creation.
- Invariant: one creation and version line per normalized requirement; each revision names the expected version; the logical version counts defined versions so the original creation stays recognizable.
- Exclusions: the program catalog and ordering (`FitzTrainingRequirementDirectory` projection), campaign pinning of launched material, content validation and storage (handler/rules), authorization.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Define (id matches tenant/program/normalized identifier) | version 1 | `TrainingRequirementDefined` |
| none | Define (id, program or identifier mismatch, non-normalized identifier) | validation refusal | none |
| none | Revise | not found | none |
| defined | Define again | conflict (handler resolves exact retry from the acceptance) | none |
| defined | Revise, expected version = current | version + 1 | `TrainingRequirementDefined` |
| defined | Revise, stale expected version | conflict | none |

## Changes (real gaps)

- `Define` trusted the caller's program and identifier. A mismatch with the requirement id surfaced as an `InvalidOperationException` from the fold during `RaiseEvent`, rather than a refusal with no fact. It now returns a Validation failure when the identifier is invalid or not already normalized, or the derived id differs from the stream id. The handler path was already guarded, so behavior for valid input is unchanged.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity, no forked stream | `ShouldKeepPublishedIdentityVectorsGivenNormalizedIdentifier`, `ShouldKeepIdentityIndependentGivenDifferentIdentifiersProgramsOrTenants` |
| Invalid decisions append nothing | `ShouldRejectWithoutAppendGivenMismatchedIdentityOrInvalidContent`, `ShouldRefuseDefinitionWithoutFactGivenIdentifierNotMatchingRequirementId` (new, aggregate level) |
| Exact retry, including after later facts | `ShouldRegisterFirstVersionAndReplayOriginalRegistrationGivenExactRetryAfterRevision` |
| Changed-intent refusal | same test (changed define content conflicts) |
| Stale revision, missing owner | `ShouldRejectStaleVersionAndKeepSoleVersionCounterGivenRevisions` |
| Bounded history | `ShouldKeepRequirementAuthorityBoundedGivenManyNativeVersions` |
| Independent requirements do not compete | `ShouldNotConflictGivenConcurrentDefinitionsOfDifferentRequirements` |
| Replay of valid history | the hydrating tests above; pinned views `ShouldKeepLaunchedMaterialGivenLaterRequirementRevision` |
| Projection consumes the fact | `ShouldProjectLatestVersionsOrderedByIdentifierGivenCatalogProjection` |

Malformed-history tests were not added, by user preference.

## Event contract status

- `TrainingRequirementDefined` (tenant, program, requirement id, identifier, version, content ref and hash, define request id, actor, changed-at, operation provenance, first response, operation result) is the sole fact. Actor and time duplicate the operation provenance by design of the native-fact envelope and are validated equal on fold. No add, rename, remove or payload change requested.

## Later-stage follow-ups (not aggregate gaps)

- Handler wiring of the selected `CommandId` receipt for `ReviseTrainingRequirement`, route authorization precedence before replay.
- Catalog directory and campaign pinning stay projection/process concerns.

## Evidence

- Jev: no request this round; the catalog entry (selected_split, immediate original result) and invariant text were sufficient.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~TrainingRequirementTests"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test calls internal helpers (`PolicyCampaignContent.Provenance`, `TrainingRequirementRules`) as the existing seed does; uncompiled.

## Review before build

Independent review, 2026-10-29. Nothing compiled or executed.

**Findings**
- Implementation: the new `Define` guard enforces the identity part of the invariant (valid, already-normalized identifier, derived id equals stream id) before `PrepareDefine`, so no mismatching fact reaches the fold. `Revise` carries program and identifier from version 1 and checks the expected version. No payload field repeats stream identity beyond the existing native-fact envelope; no event change needed.
- Test gap fixed: the theory never isolated a correct identifier with a different program (its first case changed identifier and program together). Cases are now `TR-OTHER` same program, `TR-SEC` different program, lowercase `tr-sec`, and `TR SEC`.
- Compile read: `PolicyCampaignContent.Provenance`, `TrainingRequirementRules.WriteContentAsync/Hash` and `CampaignHost` members (`Scope`, `Program`, `Tenant`, `ManagerActor`, `Clock`) exist; Core exposes internals to the tests assembly. The `CampaignHost` is `await using` and the test has Arrange/Act/Assert comments and a valid name. Not confirmed by a compiler.
- Minor note: on an already defined requirement, a mismatched Define now reports Validation before Conflict; the handler path already validates identity first, so no behavior change.

**Jev (model jev-1.14.0)**; request and response in `implementation-jev/trainingrequirement-test-impl-review-2026-10-29-*.json`
- Mismatched Define outcome: validation_refusal_no_fact 1.00 (decisive; matches code).
- Test asserts the identity invariant with no fact: noul 0.84.
- Revise consistent with sequential version line: noul 0.92.
- No catalog or event discrepancy found, so no Choice on event additions was needed.

**Remaining risks**: build and test pending; handler `CommandId` receipt and route authorization ordering are later stages.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
