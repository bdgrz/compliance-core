# ProviderNameClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `Providers` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped (tenantId, normalizedGovernedName); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Authoritative unique active name reservation.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Exact creation identity and expected provider revision; active normalized name unique within tenant.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimProviderName](../commands/provider-name-claim.md#claimprovidername) | receiver | CommandId |
| [ReleaseProviderName](../commands/provider-name-claim.md#releaseprovidername) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProviderNameClaimed](../events/provider-name-claim.md#providernameclaimed) | bdgrz.provider-name-claim.provider-name-claimed | 1 |
| [ProviderNameReleased](../events/provider-name-claim.md#providernamereleased) | bdgrz.provider-name-claim.provider-name-released | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T022](../reactors/keep-provider-names-unique-reactor.md#t022) | reaction_or_regular_process_stage | Provider | ProviderNameClaim | ClaimProviderName |
| [T023](../reactors/keep-provider-names-unique-reactor.md#t023) | reaction_or_regular_process_stage | ProviderNameClaim | Provider | ApplyProviderChange |
| [T024](../reactors/keep-provider-names-unique-reactor.md#t024) | reaction_or_regular_process_stage | Provider | ProviderNameClaim | ReleaseProviderName |
| [X003](../reactors/keep-provider-names-unique-reactor.md#x003) | planned_reaction | Provider | ProviderNameClaim | ReleaseProviderName |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/provider-name-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
