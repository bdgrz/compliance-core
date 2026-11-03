# RiskDraft

- Classification: `business_aggregate`
- Source area: `Risks` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / risks / RiskId; supplied RiskId must equal the unchanged tenant/program/normalized-identifier UUIDv5. Selected mutations use CommandId and exact authored-content revision.

**Invariant:** Normalized identifier has one creation identity/content within tenant/program and narrative updates require current revision. No additional risk-governance decision is made by this class.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CreateRiskDraft](../commands/risk-draft.md#createriskdraft) | http | RiskId |
| [ReviseRiskDraft](../commands/risk-draft.md#reviseriskdraft) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RiskDraftCreated](../events/risk-draft.md#riskdraftcreated) | bdgrz.risk.draft.created | 2 |
| [RiskDraftRevised](../events/risk-draft.md#riskdraftrevised) | bdgrz.risk.draft.revised | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/risk-draft.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
