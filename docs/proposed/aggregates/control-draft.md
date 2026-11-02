# ControlDraft

- Classification: `business_aggregate`
- Source area: `Controls` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / controls / ControlId; supplied ControlId must equal the existing tenant/program/normalized-identifier UUIDv5. Later selected commands use CommandId and the exact control/version/review bindings.

**Invariant:** Only the exact current draft or retirement can be independently reviewed then activated, and accepted successor/retirement changes one control version line without overlapping effective intervals. Responsibilities cannot create unwaived conflicts with decisions already made on the exact revision.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveControl](../commands/control-draft.md#approvecontrol) | http | CommandId |
| [CreateControlDraft](../commands/control-draft.md#createcontroldraft) | http | ControlId |
| [DesignateControlOwnerPerson](../commands/control-draft.md#designatecontrolownerperson) | command | CommandId |
| [DiscardControlDraft](../commands/control-draft.md#discardcontroldraft) | http | CommandId |
| [ProposeControlRetirement](../commands/control-draft.md#proposecontrolretirement) | http | CommandId |
| [ProposeControlSuccessor](../commands/control-draft.md#proposecontrolsuccessor) | http | CommandId |
| [RetireControl](../commands/control-draft.md#retirecontrol) | http | CommandId |
| [ReviewControl](../commands/control-draft.md#reviewcontrol) | http | CommandId |
| [ReviseControlDraft](../commands/control-draft.md#revisecontroldraft) | command | CommandId |
| [WithdrawControlProposal](../commands/control-draft.md#withdrawcontrolproposal) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ControlApproved](../events/control-draft.md#controlapproved) | bdgrz.control.approved | 2 |
| [ControlDraftCreated](../events/control-draft.md#controldraftcreated) | bdgrz.control.draft.created | 2 |
| [ControlDraftDiscarded](../events/control-draft.md#controldraftdiscarded) | bdgrz.control.draft.discarded | 2 |
| [ControlDraftRevised](../events/control-draft.md#controldraftrevised) | bdgrz.control.draft.revised | 2 |
| [ControlOwnerPersonDesignated](../events/control-draft.md#controlownerpersondesignated) | bdgrz.control.owner_person.designated | 2 |
| [ControlProposalWithdrawn](../events/control-draft.md#controlproposalwithdrawn) | bdgrz.control.proposal.withdrawn | 2 |
| [ControlRetired](../events/control-draft.md#controlretired) | bdgrz.control.retired | 2 |
| [ControlRetirementProposed](../events/control-draft.md#controlretirementproposed) | bdgrz.control.retirement.proposed | 2 |
| [ControlReviewed](../events/control-draft.md#controlreviewed) | bdgrz.control.reviewed | 2 |
| [ControlSuccessorProposed](../events/control-draft.md#controlsuccessorproposed) | bdgrz.control.successor.proposed | 2 |
| [ResponsibilityAssigned](../events/control-draft.md#responsibilityassigned) | bdgrz.responsibility.assigned | 1 |
| [ResponsibilityRevoked](../events/control-draft.md#responsibilityrevoked) | bdgrz.responsibility.revoked | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [control-draft-review-2026-10-29.md](../../product/definitive-aggregate-choreography/control-draft-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/control-draft.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
