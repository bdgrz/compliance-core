# ApplicationNameClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `[Placeholder: source capability area is not recorded]` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + normalized application name or alias (one claim stream per normalized name, same scheme as LocationNameClaim); holder is the exact DeclaredApplication stream in the same tenant.

**Invariant:** One normalized application name or alias is held by at most one active application within a tenant. Claim precedes the application fact that uses the name; release follows retirement or rename. An attributed merge retires the losing application with a merged_into reference and transfers or releases its names to the survivor (M0-D05 rules D05-09, D05-10).

**Additional decision rule:** A tenant holds one canonical name and any number of aliases per application, each normalized and claimed individually; a claim already held by a different active application is refused. Merging retires the losing application with a merged_into reference to the survivor, and its names transfer or release only with that attributed merge.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ClaimApplicationName](../commands/application-name-claim.md#claimapplicationname) | receiver | CommandId |
| [ReleaseApplicationName](../commands/application-name-claim.md#releaseapplicationname) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ApplicationNameClaimed](../events/application-name-claim.md#applicationnameclaimed) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [ApplicationNameReleased](../events/application-name-claim.md#applicationnamereleased) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X000](../reactors/keep-application-names-unique-reactor.md#x000) | planned_reaction | DeclaredApplication | ApplicationNameClaim | ClaimApplicationName |
| [X001](../reactors/keep-application-names-unique-reactor.md#x001) | planned_reaction | ApplicationNameClaim | DeclaredApplication | ApplyApplicationNameChange |
| [X002](../reactors/keep-application-names-unique-reactor.md#x002) | planned_reaction | DeclaredApplication | ApplicationNameClaim | ReleaseApplicationName |

## Product rules

- **D05-09** — An application has one canonical name and zero or more aliases, and each normalized alias is unique within the tenant. Decision: [m0-d05-application-inventory.md](../decisions/m0-d05-application-inventory.md).
- **D05-10** — Duplicate applications are resolved by an attributed merge that retires one record with a merged_into reference to the survivor. Decision: [m0-d05-application-inventory.md](../decisions/m0-d05-application-inventory.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: baseline source not recorded]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/application-name-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `PLANNED_NO_SOURCE_TYPE`.
