# OperationalProcessNameClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (tenantId, normalizedName); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Authoritative active normalized-name uniqueness.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Process content/version is checked against exact prior revision; inactive names may be reused only when policy permits.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimOperationalProcessName](../commands/operational-process-name-claim.md#claimoperationalprocessname) | receiver | CommandId |
| [ReleaseOperationalProcessName](../commands/operational-process-name-claim.md#releaseoperationalprocessname) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [OperationalProcessNameClaimed](../events/operational-process-name-claim.md#operationalprocessnameclaimed) | bdgrz.operational-process-name-claim.operational-process-name-claimed | 1 |
| [OperationalProcessNameReleased](../events/operational-process-name-claim.md#operationalprocessnamereleased) | bdgrz.operational-process-name-claim.operational-process-name-released | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T028](../reactors/operational-process-change-accepted-reactor.md#t028) | reaction_or_regular_process_stage | OperationalProcess | OperationalProcessNameClaim | ClaimOperationalProcessName |
| [T029](../reactors/operational-process-change-accepted-reactor.md#t029) | reaction_or_regular_process_stage | OperationalProcessNameClaim | OperationalProcess | ApplyOperationalProcessChange |
| [T030](../reactors/operational-process-change-accepted-reactor.md#t030) | reaction_or_regular_process_stage | OperationalProcess | OperationalProcessNameClaim | ReleaseOperationalProcessName |
| [X004](../reactors/operational-process-change-accepted-reactor.md#x004) | planned_reaction | OperationalProcess | OperationalProcessNameClaim | ReleaseOperationalProcessName |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [operational-process-name-claim-review-2026-10-27.md](../../product/definitive-aggregate-choreography/operational-process-name-claim-review-2026-10-27.md)
- Full selected record: [JSON](../data/aggregates/operational-process-name-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
