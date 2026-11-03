# OperationalProcess

- Classification: `business_aggregate`
- Source area: `TechnologyInventory` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped processId; `OperationalProcessId` on `RecordOperationalProcess` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Independent governed operational process lifecycle. Retirement is a distinct terminal fact (planned OperationalProcessRetired), not a value inside a revision

**Additional decision rule:** Stable identity and type are owned here; no other context re-creates a Location or OperationalProcess, and a written procedure is policy content, never an OperationalProcess record.

**Must preserve:** Process content/version is checked against exact prior revision; inactive names may be reused only when policy permits.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordOperationalProcess](../commands/operational-process.md#recordoperationalprocess) | http | OperationalProcessId |
| [ReviseOperationalProcess](../commands/operational-process.md#reviseoperationalprocess) | http | CommandId |
| [CancelOperationalProcessChange](../commands/operational-process.md#canceloperationalprocesschange) | abort | CommandId |
| [ApplyOperationalProcessChange](../commands/operational-process.md#applyoperationalprocesschange) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [OperationalProcessChangeAccepted](../events/operational-process.md#operationalprocesschangeaccepted) | bdgrz.operational-process.operational-process-change-accepted | 1 |
| [OperationalProcessChangeCanceled](../events/operational-process.md#operationalprocesschangecanceled) | bdgrz.operational-process.operational-process-change-canceled | 1 |
| [OperationalProcessRetired](../events/operational-process.md#operationalprocessretired) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [OperationalProcessRevisionRecorded](../events/operational-process.md#operationalprocessrevisionrecorded) | bdgrz.inventory.operational_process.revision_recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T028](../reactors/keep-operational-process-names-unique-reactor.md#t028) | reaction_or_regular_process_stage | OperationalProcess | OperationalProcessNameClaim | ClaimOperationalProcessName |
| [T029](../reactors/keep-operational-process-names-unique-reactor.md#t029) | reaction_or_regular_process_stage | OperationalProcessNameClaim | OperationalProcess | ApplyOperationalProcessChange |
| [T030](../reactors/keep-operational-process-names-unique-reactor.md#t030) | reaction_or_regular_process_stage | OperationalProcess | OperationalProcessNameClaim | ReleaseOperationalProcessName |
| [A016](../reactors/unassigned-domain-choreography.md#a016) | process_abort_binding | OperationalProcess | OperationalProcess | CancelOperationalProcessChange |
| [X004](../reactors/keep-operational-process-names-unique-reactor.md#x004) | planned_reaction | OperationalProcess | OperationalProcessNameClaim | ReleaseOperationalProcessName |

## Product rules

- **D22-06** — Location and OperationalProcess are owned by technology and information inventory with stable identity and type; no other context silently re-creates them, and a written procedure is policy content, not an OperationalProcess record. Decision: [m0-d22-canonical-ownership.md](../decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/operational-process.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
