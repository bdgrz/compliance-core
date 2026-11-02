# WorkItemAssignment

- Classification: `business_aggregate`
- Source area: `Work` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / work-item-assignments / existing UUIDv5 of ProgramId and WorkItemId. Selected CommandId binds original actor, exact source work identity and assignment episode; imported ledger history is read-only provenance.

**Invariant:** One source-derived work item has current assignee/escalation revision; escalation is accepted at most once, assignment commands require expected revision. Source eligibility and completion belong to producing workflow.

**Additional decision rule:** Assignment is attributed and refuses a person the source workflow would reject (separation of duties), an unassigned team item is claimed only by a team member holding the source-required role, assign and reassign are limited to Compliance Lead or Org Admin, delegate to the current assignee and only to someone eligible, and escalate by any assignee or the system. The identity of a finding-closure item includes the exact finding revision, so an assignment against an earlier revision cannot authorize the new round, and a system escalation after 7 days overdue targets the Compliance Lead and adds visibility without reassigning.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssignWorkItem](../commands/work-item-assignment.md#assignworkitem) | http | CommandId |
| [ClaimWorkItem](../commands/work-item-assignment.md#claimworkitem) | http | CommandId |
| [DelegateWorkItem](../commands/work-item-assignment.md#delegateworkitem) | http | CommandId |
| [EscalateWorkItem](../commands/work-item-assignment.md#escalateworkitem) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkAssignmentLegacyHistoryImported](../events/work-item-assignment.md#workassignmentlegacyhistoryimported) | bdgrz.work.assignment-history-imported | 2 |
| [WorkItemAssignmentEscalated](../events/work-item-assignment.md#workitemassignmentescalated) | bdgrz.work.escalated | 2 |
| [WorkItemAssignmentRecorded](../events/work-item-assignment.md#workitemassignmentrecorded) | bdgrz.work.assigned | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D15-05** — The identity of a finding-closure work item includes the exact finding revision; revising or reopening a finding creates a new work identity, and an assignment made against an earlier revision cannot authorize or assign the new round. Source: [m0-d15-work-queue.md](../../product/decisions/m0-d15-work-queue.md).
- **D15-09** — An unassigned team work item may be claimed only by a member of that team holding the role the source workflow requires; assign and reassign are limited to a Compliance Lead or Org Admin; delegate is limited to the current assignee and only to someone eligible under the source workflow; escalate may be done by any assignee or by the system. Source: [m0-d15-work-queue.md](../../product/decisions/m0-d15-work-queue.md).
- **D15-10** — Every work-item assignment action is attributed and checked against separation-of-duties rules; an item cannot be assigned to a person the source workflow would reject. Source: [m0-d15-work-queue.md](../../product/decisions/m0-d15-work-queue.md).
- **D15-11** — A work item that is 7 days overdue escalates automatically to the Compliance Lead, recorded as a system-actor escalation; escalation adds visibility only and does not reassign the item. Source: [m0-d15-work-queue.md](../../product/decisions/m0-d15-work-queue.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/work-item-assignment.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
