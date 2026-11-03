# ManagementAssertion

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / management-assertions / AssertionId, with immutable ProgramId/examination scope. AssertionId identifies creation; selected mutations use CommandId and exact asserted version/source proofs.

**Invariant:** One exact current authored assertion version receives at most one immutable independent named CEO/CISO decision; revision creates changed wording/input bindings.

**Additional decision rule:** The assertion decision is signed only by the CEO or the CISO as an attributable named decision on an exact package version; the representation letter has the same signer rule, owned by its own aggregate.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveManagementAssertion](../commands/management-assertion.md#approvemanagementassertion) | command | CommandId |
| [CreateManagementAssertion](../commands/management-assertion.md#createmanagementassertion) | http | AssertionId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ManagementAssertionApproved](../events/management-assertion.md#managementassertionapproved) | bdgrz.examinations.management-assertion-approved | 1 |
| [ManagementAssertionRecorded](../events/management-assertion.md#managementassertionrecorded) | bdgrz.examinations.management-assertion-recorded | 2 |
| [ManagementAssertionRevised](../events/management-assertion.md#managementassertionrevised) | bdgrz.examinations.management-assertion-revised | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D17-06** — The management assertion and the representation letter are each signed by the CEO or the CISO, recorded as an attributable sign-off decision on an exact package version. Decision: [m0-d17-audit-deliverables.md](../decisions/m0-d17-audit-deliverables.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/management-assertion.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
