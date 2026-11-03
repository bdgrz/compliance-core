# DeclaredSystemInstance

- Classification: `business_aggregate`
- Source area: `Applications` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + caller SystemInstanceId with immutable owning application/program scope.

**Invariant:** One system instance has one immutable application parent and registered identity; metadata revisions and terminal retirement follow its own expected revision.

**Additional decision rule:** Instance kind must be one of cloud_account, source_code_organization, identity_provider_tenant, saas_tenant, environment, or other; other requires a description, and platform is required for every kind except environment and other. An active instance's provider identifier is unique per tenant across (kind, platform, provider identifier), so same-named instances of different kinds or platforms do not collide. Access campaigns reference an exact instance and never create or own it.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DeclareSystemInstance](../commands/declared-system-instance.md#declaresysteminstance) | http | SystemInstanceId |
| [RetireSystemInstance](../commands/declared-system-instance.md#retiresysteminstance) | http | CommandId |
| [ReviseSystemInstance](../commands/declared-system-instance.md#revisesysteminstance) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [SystemInstanceRegistered](../events/declared-system-instance.md#systeminstanceregistered) | bdgrz.system_instance.registered | 1 |
| [SystemInstanceRetired](../events/declared-system-instance.md#systeminstanceretired) | bdgrz.system_instance.retired | 1 |
| [SystemInstanceRevised](../events/declared-system-instance.md#systeminstancerevised) | bdgrz.applications.system-instance-revised | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D05-04** — A SystemInstance's instance_kind is one of cloud_account, source_code_organization, identity_provider_tenant, saas_tenant, environment, or other; the other kind requires a description, and platform is required for every kind except environment and other. Decision: [m0-d05-application-inventory.md](../decisions/m0-d05-application-inventory.md).
- **D05-05** — An active SystemInstance's provider identifier is unique per tenant for the combination of instance kind, platform, and provider identifier, so same-named instances of different kinds or platforms do not collide. Decision: [m0-d05-application-inventory.md](../decisions/m0-d05-application-inventory.md).
- **D22-01** — Application and SystemInstance are owned solely by application inventory; access review campaigns reference an exact system instance, and a campaign's review inclusion neither creates nor owns the instance (ReviewedSystem is a legacy name, not a second entity). Decision: [m0-d22-canonical-ownership.md](../decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/declared-system-instance.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
