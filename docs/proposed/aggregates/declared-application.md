# DeclaredApplication

- Classification: `business_aggregate`
- Source area: `Applications` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped applicationId; `ApplicationId` on `DeclareApplication` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Application creation, metadata revision, retirement and import admission generation. Name and alias uniqueness is decided by ApplicationNameClaim, which the application must hold before using a name; a merge retires this application with a merged_into reference to the survivor (M0-D05 D05-10)

**Additional decision rule:** Refuses an active application without a governed name, application kind, lifecycle, and criticality, or without a system owner and an access owner, both workforce Person references that need not be platform members. Retirement is effective-dated and requires a reason; it never deletes history or any snapshot reference to the application.

**Must preserve:** Retirement requires an approved successor for exact app revision; import holds block incompatible local change.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApplyApplicationImportEffect](../commands/declared-application.md#applyapplicationimporteffect) | receiver | CommandId |
| [DeclareApplication](../commands/declared-application.md#declareapplication) | http | ApplicationId |
| [RetireApplication](../commands/declared-application.md#retireapplication) | http | CommandId |
| [ReviseApplication](../commands/declared-application.md#reviseapplication) | http | CommandId |
| [ReserveApplicationImportTarget](../commands/declared-application.md#reserveapplicationimporttarget) | receiver | CommandId |
| [FenceApplicationImportTarget](../commands/declared-application.md#fenceapplicationimporttarget) | receiver | CommandId |
| [ReleaseApplicationImportTarget](../commands/declared-application.md#releaseapplicationimporttarget) | receiver | CommandId |
| [ApplyApplicationNameChange](../commands/declared-application.md#applyapplicationnamechange) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ApplicationDeclared](../events/declared-application.md#applicationdeclared) | bdgrz.application.declared | 1 |
| [ApplicationImportEffectCanceled](../events/declared-application.md#applicationimporteffectcanceled) | bdgrz.declared_application.import_effect_canceled | 1 |
| [ApplicationImportEffectPending](../events/declared-application.md#applicationimporteffectpending) | bdgrz.application_import.effect_pending | 2 |
| [ApplicationImportTargetAuthorized](../events/declared-application.md#applicationimporttargetauthorized) | bdgrz.declared_application.import_target_authorized | 1 |
| [ApplicationImportTargetSettled](../events/declared-application.md#applicationimporttargetsettled) | bdgrz.declared_application.import_target_settled | 1 |
| [ApplicationRetired](../events/declared-application.md#applicationretired) | bdgrz.application.retired | 1 |
| [ApplicationRevised](../events/declared-application.md#applicationrevised) | bdgrz.application.revised | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T015](../reactors/import-source-admission-reactor.md#t015) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | ReserveApplicationImportTarget |
| [T016](../reactors/import-source-admission-reactor.md#t016) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | ApplyApplicationImportEffect |
| [T018](../reactors/import-cancellation-reactor.md#t018) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | FenceApplicationImportTarget |
| [T021](../reactors/import-source-admission-reactor.md#t021) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | ReleaseApplicationImportTarget |
| [X000](../reactors/application-name-change-reactor.md#x000) | planned_reaction | DeclaredApplication | ApplicationNameClaim | ClaimApplicationName |
| [X001](../reactors/application-name-change-reactor.md#x001) | planned_reaction | ApplicationNameClaim | DeclaredApplication | ApplyApplicationNameChange |
| [X002](../reactors/application-name-change-reactor.md#x002) | planned_reaction | DeclaredApplication | ApplicationNameClaim | ReleaseApplicationName |
| [X005](../reactors/application-retirement-reactor.md#x005) | planned_reaction | DeclaredApplication | ApplicationRelationship | RemoveApplicationRelationship |

## Product rules

- **D05-02** — Every active application has a governed name, application kind, lifecycle, criticality, a system owner, and an access owner, and both owners are workforce Person references who need not be platform members. Source: [m0-d05-application-inventory.md](../../product/decisions/m0-d05-application-inventory.md).
- **D05-11** — Retiring an application is effective-dated, requires a reason, and never deletes history or any snapshot reference to it. Source: [m0-d05-application-inventory.md](../../product/decisions/m0-d05-application-inventory.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [declared-application-review-2026-10-29.md](../../product/definitive-aggregate-choreography/declared-application-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/declared-application.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
