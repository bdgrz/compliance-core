# ExaminationSignOff

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / examination-signoffs / SignOffId, with exact examination, package/snapshot, signer and role binding. SignOffId is the selected immutable creation identity.

**Invariant:** One final personal management decision binds exact package/assertion/letter/outcome and acknowledges exactly every unresolved material item.

**Additional decision rule:** Refuses out-of-order package sign-off: the package must be validated first, then the assertion signed, then the representation letter signed, and only then is the outcome recorded.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordManagementExaminationReview](../commands/examination-sign-off.md#recordmanagementexaminationreview) | http | SignOffId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ExaminationSignedOff](../events/examination-sign-off.md#examinationsignedoff) | bdgrz.examinations.examination-signed-off | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X008](../reactors/unassigned-architecture-rows.md#x008) | proof_read_at_command | ManagementRepresentationLetter | ExaminationSignOff | SignOffExamination |

## Product rules

- **D17-07** — Package sign-off sequence is fixed: the package is validated, then the assertion is signed, then the representation letter is signed, then the outcome is recorded. Source: [m0-d17-audit-deliverables.md](../../product/decisions/m0-d17-audit-deliverables.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [examination-sign-off-review-2026-10-29.md](../../product/definitive-aggregate-choreography/examination-sign-off-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/examination-sign-off.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
