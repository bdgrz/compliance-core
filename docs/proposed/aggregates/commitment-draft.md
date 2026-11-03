# CommitmentDraft

- Classification: `business_aggregate`
- Source area: `Commitments` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + ProgramId + normalized kind/identifier retain the existing deterministic UUIDv5 owner algorithm. Supplied DraftId must equal that existing identity; later selected commands use CommandId and the exact draft/version/review bindings.

**Invariant:** One tenant/program/kind/identifier has immutable identity/source creation, revisioned authored content, exact verified-source accepted review and independent approval that publishes strictly later effective versions; retired commitment remains terminal.

**Additional decision rule:** A draft is one of service_commitment, system_requirement, user_entity_responsibility or subservice_responsibility and carries an identifier, statement, governed service, typed source citation (artifact kind, title, version or date, section locator, optional artifact id; no pasted contract text), owner Person and applicable Trust Services categories; a provider reference is required for subservice_responsibility and refused for other kinds. Categories are validated against the program's current approved boundary version on create, revise and approve, and approval records that boundary version id. Approval is by a management approver (an Org Admin or Compliance Lead designated for the program), attributed and tied to the exact commitment and boundary version; self-approval of one's own draft needs a separation-of-duties exception.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveCommitmentDraft](../commands/commitment-draft.md#approvecommitmentdraft) | http | CommandId |
| [CreateCommitmentDraft](../commands/commitment-draft.md#createcommitmentdraft) | http | DraftId |
| [RetireCommitment](../commands/commitment-draft.md#retirecommitment) | command | CommandId |
| [ReviewCommitmentDraft](../commands/commitment-draft.md#reviewcommitmentdraft) | http | CommandId |
| [ReviseCommitmentDraft](../commands/commitment-draft.md#revisecommitmentdraft) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CommitmentApproved](../events/commitment-draft.md#commitmentapproved) | bdgrz.commitment.approved | 2 |
| [CommitmentDraftCreated](../events/commitment-draft.md#commitmentdraftcreated) | bdgrz.commitment.draft.created | 2 |
| [CommitmentDraftRevised](../events/commitment-draft.md#commitmentdraftrevised) | bdgrz.commitment.draft.revised | 2 |
| [CommitmentRetired](../events/commitment-draft.md#commitmentretired) | bdgrz.commitments.commitment-retired | 2 |
| [CommitmentReviewed](../events/commitment-draft.md#commitmentreviewed) | bdgrz.commitment.reviewed | 2 |
| [ResponsibilityAssigned](../events/commitment-draft.md#responsibilityassigned) | bdgrz.responsibility.assigned | 1 |
| [ResponsibilityRevoked](../events/commitment-draft.md#responsibilityrevoked) | bdgrz.responsibility.revoked | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D09-01** — Each commitment is one of four kinds (service_commitment, system_requirement, user_entity_responsibility, subservice_responsibility) and carries an identifier, statement, service reference, source citation, owner Person, applicable Trust Services categories, and lifecycle status of draft, approved, or retired. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).
- **D09-02** — A commitment identifier is unique per tenant, program, and commitment kind. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).
- **D09-03** — A commitment's source citation is a typed reference (artifact kind, title, version or date, section locator, optional governed artifact id), not pasted contract text. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).
- **D09-04** — A commitment's categories are validated against the program's current approved boundary version when the commitment is created, revised, or approved, and the approval records that boundary version id. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).
- **D09-06** — A commitment must be approved by a management approver, an Org Admin or Compliance Lead designated as the program's management approver; the approval is attributed, versioned, references the exact commitment version and boundary version, and self-approval of one's own draft requires a separation-of-duties exception. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).
- **D09-07** — A subservice_responsibility commitment requires a provider reference to the Provider that performs it. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/commitment-draft.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
