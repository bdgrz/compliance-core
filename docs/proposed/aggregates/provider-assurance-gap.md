# ProviderAssuranceGap

- Classification: `business_aggregate`
- Source area: `Providers` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped (tenantId, providerId, gapId); `GapId` on `RecordProviderCoverageGap` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One current assurance gap with exact source-version and closure lineage.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Risk adoption cannot close a gap or occur after closure; each review binds same provider/current report.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CloseProviderCoverageGap](../commands/provider-assurance-gap.md#closeprovidercoveragegap) | http | CommandId |
| [LinkProviderCoverageGapRiskAcceptance](../commands/provider-assurance-gap.md#linkprovidercoveragegapriskacceptance) | http | CommandId |
| [RecordProviderCoverageGap](../commands/provider-assurance-gap.md#recordprovidercoveragegap) | http | GapId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProviderCoverageGapClosed](../events/provider-assurance-gap.md#providercoveragegapclosed) | bdgrz.provider.coverage_gap.closed | 1 |
| [ProviderCoverageGapRecorded](../events/provider-assurance-gap.md#providercoveragegaprecorded) | bdgrz.provider.coverage_gap.recorded | 1 |
| [ProviderCoverageGapRiskAcceptanceAdmitted](../events/provider-assurance-gap.md#providercoveragegapriskacceptanceadmitted) | bdgrz.provider-assurance-gap.provider-coverage-gap-risk-acceptance-admitted | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T088](../reactors/keep-risk-acceptance-with-provider-gap-reactor.md#t088) | reaction_or_regular_process_stage | ProviderAssuranceGap | ProviderRiskAcceptance | LinkProviderRiskAcceptanceToGap |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/provider-assurance-gap.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
