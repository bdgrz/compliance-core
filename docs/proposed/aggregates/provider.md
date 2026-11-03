# Provider

- Classification: `business_aggregate`
- Source area: `Providers` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped providerId; `ProviderId` on `RecordProvider` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One provider content, version and retirement lifecycle. Retirement is a distinct terminal fact (planned ProviderRetired), not a value inside a revision

**Additional decision rule:** Materiality is recorded with a basis drawn only from customer_data and/or critical_path (stores or processes customer data, or is on the critical path for an in-scope service) and a rationale; spend is never a basis.

**Must preserve:** Exact creation identity and expected provider revision; active normalized name unique within tenant.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordProvider](../commands/provider.md#recordprovider) | http | ProviderId |
| [ReviseProvider](../commands/provider.md#reviseprovider) | http | CommandId |
| [CancelProviderChange](../commands/provider.md#cancelproviderchange) | abort | CommandId |
| [ApplyProviderChange](../commands/provider.md#applyproviderchange) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ProviderChangeAccepted](../events/provider.md#providerchangeaccepted) | bdgrz.provider.provider-change-accepted | 1 |
| [ProviderChangeCanceled](../events/provider.md#providerchangecanceled) | bdgrz.provider.provider-change-canceled | 1 |
| [ProviderRecorded](../events/provider.md#providerrecorded) | bdgrz.provider.recorded | 1 |
| [ProviderRetired](../events/provider.md#providerretired) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [ProviderRevised](../events/provider.md#providerrevised) | bdgrz.provider.revised | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T022](../reactors/keep-provider-names-unique-reactor.md#t022) | reaction_or_regular_process_stage | Provider | ProviderNameClaim | ClaimProviderName |
| [T023](../reactors/keep-provider-names-unique-reactor.md#t023) | reaction_or_regular_process_stage | ProviderNameClaim | Provider | ApplyProviderChange |
| [T024](../reactors/keep-provider-names-unique-reactor.md#t024) | reaction_or_regular_process_stage | Provider | ProviderNameClaim | ReleaseProviderName |
| [A014](../reactors/unassigned-domain-choreography.md#a014) | process_abort_binding | Provider | Provider | CancelProviderChange |
| [X003](../reactors/keep-provider-names-unique-reactor.md#x003) | planned_reaction | Provider | ProviderNameClaim | ReleaseProviderName |

## Product rules

- **D11-01** — A provider is material when it stores or processes customer data or is on the critical path for an in-scope service; spend is irrelevant, and the provider records materiality, its basis (customer_data and/or critical_path), and a rationale. Decision: [m0-d11-vendors.md](../decisions/m0-d11-vendors.md).
- **D11-06** — A subservice provider must declare a boundary treatment of carve_out or inclusive; the default is carve_out and inclusive requires an explicit reasoned exception. Decision: [m0-d11-vendors.md](../decisions/m0-d11-vendors.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/provider.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
