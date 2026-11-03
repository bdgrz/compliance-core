# DataFlow

- Classification: `business_aggregate`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + caller DataFlowId under the preserved shared technology-inventory address/sentinel scheme.

**Invariant:** One tenant inventory identity has one record kind; flow endpoint/asset scope and encryption or approved-exception terms must match highest admitted carried classification, and effective date never moves backward.

**Additional decision rule:** Requires the flow to run from a service or system instance to a data store or external party, carry one or more information assets, a purpose, and explicit encrypted_in_transit and encrypted_at_rest flags, and be effective-dated and versioned. A data flow records where information starts and goes, which information it carries, why it moves, and how it is protected. Its effective date cannot move backward. Confidential or restricted information must be encrypted both when stored and sent, unless an approved exception applies.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** A new data flow is active. ReviseDataFlow may set it to active or retired while preserving its source, destination, information, protection, and effective-date rules. Changing its status does not automatically change related asset or technology records, and a retired flow is not permanently closed by this rule.

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordDataFlow](../commands/data-flow.md#recorddataflow) | http | DataFlowId |
| [ReviseDataFlow](../commands/data-flow.md#revisedataflow) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [DataFlowRevisionRecorded](../events/data-flow.md#dataflowrevisionrecorded) | bdgrz.inventory.data_flow.revision_recorded | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D08-05** — A DataFlow runs from a service or system instance to a data store or external party, carries one or more information assets, a purpose, and encrypted_in_transit and encrypted_at_rest flags, and is effective-dated and versioned. Source: [m0-d08-technology-inventory.md](../../product/decisions/m0-d08-technology-inventory.md).
- **D08-06** — A DataFlow is invalid when any carried information asset is confidential or restricted and either encryption flag is false, unless an approved exception is referenced. Source: [m0-d08-technology-inventory.md](../../product/decisions/m0-d08-technology-inventory.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [data-flow-review-2026-10-29.md](../../product/definitive-aggregate-choreography/data-flow-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/data-flow.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
