# ApplicationImportSource

- Classification: `business_governor`
- Source area: `Applications` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + EXACT validated SourceKey + EXACT validated namespace under unchanged length-prefixed ApplicationImportSource.IdFor. Validation rejects surrounding whitespace/oversize values; there is no case folding, Unicode normalization or source-key normalization. Existing SourceRecordId ordinal equality and mapping publication identity remain exact.

**Invariant:** One exact tenant/source-key/namespace authority serializes batch admission and durable source-record mapping publication. Complete verified published-batch order and frozen SourceClaims define mutable replace/remove mappings; source revision/generation fences stale plans and omissions. There is no independent ApplicationSourceClaim root.

**Additional decision rule:** A source record is mapped to an application only by exact source key and namespace; a record that would resolve to more than one application or system identity is refused or left unmapped, never silently merged.

**Must preserve:** Freeze complete rows and expected revisions. No partial batch visibility. Target owner replay uses accepted native facts without live coordinator lookup. Staged observation and execution share one ApplicationImportBatch revision; old ImportBatch is a cutover predecessor only.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimApplicationImportSource](../commands/application-import-source.md#claimapplicationimportsource) | receiver | CommandId |
| [ReleaseApplicationImportSource](../commands/application-import-source.md#releaseapplicationimportsource) | reactor | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ApplicationImportSourceAdmissionRecorded](../events/application-import-source.md#applicationimportsourceadmissionrecorded) | bdgrz.application_import_source.admission_recorded | 1 |
| [ApplicationImportSourceBatchCompleted](../events/application-import-source.md#applicationimportsourcebatchcompleted) | bdgrz.application_import_source.settled | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T014](../reactors/import-source-admission-reactor.md#t014) | reaction_or_regular_process_stage | ApplicationImportBatch | ApplicationImportSource | ClaimApplicationImportSource |
| [T020](../reactors/import-source-admission-reactor.md#t020) | reaction_or_regular_process_stage | ApplicationImportBatch | ApplicationImportSource | ReleaseApplicationImportSource |

## Product rules

- **D20-05** — Records matched from sources must not silently merge identities or systems. Source: [m0-d20-integration-sources.md](../../product/decisions/m0-d20-integration-sources.md).

## Adopted architecture decisions

- `OWNER_ApplicationSourceClaim` — absorb_in_source_authority.

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [application-import-source-review-2026-10-29.md](../../product/definitive-aggregate-choreography/application-import-source-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/application-import-source.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
