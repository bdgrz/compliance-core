# TechnologyComponent

- Classification: `business_aggregate`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller ComponentId using preserved TechnologyInventoryStreams identity; cloud-account identity remains bound to its selected system instance.

**Invariant:** One tenant inventory identity has exactly one record kind; a component keeps immutable category/system-instance binding, cloud account keyed to its unique instance, and category-specific valid content/lifecycle.

**Additional decision rule:** Inventory is at account and environment granularity with categories cloud_account, environment, network, data_store, repository and endpoint_class, never per resource; each component carries a governed name, owner, environment reference, location reference and lifecycle. A cloud_account component shares its SystemInstance identity one-to-one, and an endpoint_class holds a count and management source, never individual devices. Keep one record for each governed technology component at the level it is managed. A cloud account belongs to exactly one system instance. Endpoint categories are recorded by count and management source.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** A new component is active. ReviseTechnologyComponent may set it to active or retired. Retirement keeps the same component identity and historical references. This change alone does not change the system instance or access.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordTechnologyComponent](../commands/technology-component.md#recordtechnologycomponent) | http | ComponentId |
| [ReviseTechnologyComponent](../commands/technology-component.md#revisetechnologycomponent) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TechnologyComponentRevisionRecorded](../events/technology-component.md#technologycomponentrevisionrecorded) | bdgrz.inventory.component.revision_recorded | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D08-01** — The technology inventory is recorded at account and environment granularity with component categories cloud_account, environment, network, data_store, repository, and endpoint_class, not per resource; each component has a governed name, owner, environment reference, location reference, and lifecycle. Source: [m0-d08-technology-inventory.md](../../product/decisions/m0-d08-technology-inventory.md).
- **D08-02** — A cloud_account infrastructure component corresponds one-to-one with a SystemInstance of kind cloud_account and must not be duplicated. Source: [m0-d08-technology-inventory.md](../../product/decisions/m0-d08-technology-inventory.md).
- **D08-03** — An endpoint class is a class record with a count and a management source; individual devices are not inventoried. Source: [m0-d08-technology-inventory.md](../../product/decisions/m0-d08-technology-inventory.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/technology-component.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
