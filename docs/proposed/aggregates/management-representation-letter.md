# ManagementRepresentationLetter

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / management-representation-letters / LetterId, with exact examination and document content. LetterId identifies creation; selected signature mutations use CommandId with the exact signer and version.

**Invariant:** Letter becomes signed exactly when every fixed named required signer has one immutable distinct signed artifact; no unlisted signer may sign.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordManagementRepresentationLetter](../commands/management-representation-letter.md#recordmanagementrepresentationletter) | http | LetterId |
| [SignManagementRepresentationLetter](../commands/management-representation-letter.md#signmanagementrepresentationletter) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ManagementRepresentationLetterSigned](../events/management-representation-letter.md#managementrepresentationlettersigned) | bdgrz.examinations.management-representation-letter-signed | 1 |
| [RepresentationLetterRecorded](../events/management-representation-letter.md#representationletterrecorded) | bdgrz.examinations.representation-letter-recorded | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X008](../reactors/unassigned-domain-choreography.md#x008) | proof_read_at_command | ManagementRepresentationLetter | ExaminationSignOff | SignOffExamination |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/management-representation-letter.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
