# ServiceIdentity

- Classification: `business_aggregate`
- Source area: `Workforce` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller ServiceIdentityId; creation uses that identity and revisions use caller CommandId.

**Invariant:** One governed nonhuman identity has exactly one accountable person/team owner, approved purpose, valid lifecycle/expiry and review date within one year.

**Additional decision rule:** Owner kind is a person or a team and never free text or a group or role converted into a non-human identity; identity kind is workload, service, automation, bot or integration, lifecycle is active, disabled or retired, and an active identity cannot already be expired. A shared account additionally requires an accountable owner person and a shared justification and is never treated as human.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordServiceIdentity](../commands/service-identity.md#recordserviceidentity) | http | ServiceIdentityId |
| [ReviseServiceIdentity](../commands/service-identity.md#reviseserviceidentity) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ServiceIdentityRecorded](../events/service-identity.md#serviceidentityrecorded) | bdgrz.workforce.service-identity.recorded | 1 |
| [ServiceIdentityRevised](../events/service-identity.md#serviceidentityrevised) | bdgrz.workforce.service-identity.revised | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D06-08** — Every non-human identity (ServiceIdentity) has exactly one accountable owner, either a person or a team, plus an approved purpose and a review date no more than one year in the future. Source: [m0-d06-workforce-source.md](../../product/decisions/m0-d06-workforce-source.md).
- **D22-03** — ServiceIdentity (non-human identity) is owned by workforce assurance with an accountable owner relationship; a group or role never becomes a non-human identity. Source: [m0-d22-canonical-ownership.md](../../product/decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [service-identity-review-2026-10-27.md](../../product/definitive-aggregate-choreography/service-identity-review-2026-10-27.md)
- Full selected record: [JSON](../data/aggregates/service-identity.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
