# ClientService

- Classification: `business_aggregate`
- Source area: `Programs` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + caller ServiceId; creation uses that identity and mutations use caller CommandId.

**Invariant:** Once retired a service cannot be revised, and its program identity cannot be rebound; create identity is immutable.

**Additional decision rule:** Each new service must belong to a program and be distinct from applications and firm ServiceEngagements; a boundary draft may cite only active services owned by the same program. Legacy services without a program stay readable but are refused as governed scope references.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CreateClientService](../commands/client-service.md#createclientservice) | http | ServiceId |
| [RetireClientService](../commands/client-service.md#retireclientservice) | http | CommandId |
| [ReviseClientService](../commands/client-service.md#reviseclientservice) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ClientServiceCreated](../events/client-service.md#clientservicecreated) | bdgrz.client-service.created | 2 |
| [ClientServiceRetired](../events/client-service.md#clientserviceretired) | bdgrz.client-service.retired | 2 |
| [ClientServiceRevised](../events/client-service.md#clientservicerevised) | bdgrz.client-service.revised | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D22-04** — ClientService is owned by program and scope, distinct from an application and from a firm ServiceEngagement; each new service belongs to a program, and a boundary draft may cite only active services owned by that same program. Decision: [m0-d22-canonical-ownership.md](../decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/client-service.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
