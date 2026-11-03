# LocationNameClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** (tenantId, locationKind, normalizedName)

**Invariant:** Authoritative active-name uniqueness claim.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Kind is immutable; only active same-kind names are unique.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimLocationName](../commands/location-name-claim.md#claimlocationname) | receiver | CommandId |
| [ReleaseLocationName](../commands/location-name-claim.md#releaselocationname) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [LocationNameClaimed](../events/location-name-claim.md#locationnameclaimed) | bdgrz.location-name-claim.location-name-claimed | 1 |
| [LocationNameReleased](../events/location-name-claim.md#locationnamereleased) | bdgrz.location-name-claim.location-name-released | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T025](../reactors/keep-location-names-unique-reactor.md#t025) | reaction_or_regular_process_stage | Location | LocationNameClaim | ClaimLocationName |
| [T026](../reactors/keep-location-names-unique-reactor.md#t026) | reaction_or_regular_process_stage | LocationNameClaim | Location | ApplyLocationChange |
| [T027](../reactors/keep-location-names-unique-reactor.md#t027) | reaction_or_regular_process_stage | Location | LocationNameClaim | ReleaseLocationName |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/location-name-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
