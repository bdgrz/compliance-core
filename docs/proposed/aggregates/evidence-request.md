# EvidenceRequest

- Classification: `business_aggregate`
- Source area: `Evidence` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped requestId; `EvidenceRequestId` on `OpenEvidenceRequest` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One open-to-fulfilled/cancelled lifecycle.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Exactly one terminal transition; require exact source/delivery proof as appropriate.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CancelEvidenceRequest](../commands/evidence-request.md#cancelevidencerequest) | http | CommandId |
| [FulfilEvidenceRequest](../commands/evidence-request.md#fulfilevidencerequest) | http | CommandId |
| [OpenEvidenceRequest](../commands/evidence-request.md#openevidencerequest) | http | EvidenceRequestId |
| [AuthorizeEvidenceRequestActionCompletion](../commands/evidence-request.md#authorizeevidencerequestactioncompletion) | receiver | CommandId |
| [CloseEvidenceRequestActionCompletionAuthorization](../commands/evidence-request.md#closeevidencerequestactioncompletionauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [EvidenceRequestActionCompletionAuthorized](../events/evidence-request.md#evidencerequestactioncompletionauthorized) | bdgrz.evidence-request.evidence-request-action-completion-use-authorized | 1 |
| [EvidenceRequestActionCompletionAuthorizationClosed](../events/evidence-request.md#evidencerequestactioncompletionauthorizationclosed) | bdgrz.evidence-request.evidence-request-action-completion-use-settled | 1 |
| [EvidenceRequestCancelled](../events/evidence-request.md#evidencerequestcancelled) | bdgrz.evidence.request.cancelled | 2 |
| [EvidenceRequestFulfilled](../events/evidence-request.md#evidencerequestfulfilled) | bdgrz.evidence.request.fulfilled | 2 |
| [EvidenceRequestOpened](../events/evidence-request.md#evidencerequestopened) | bdgrz.evidence.request.opened | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T071](../reactors/risk-action-completion-review-reactor.md#t071) | reaction_or_regular_process_stage | RiskTreatmentAction | EvidenceRequest | AuthorizeEvidenceRequestActionCompletion |
| [T072](../reactors/risk-action-completion-review-reactor.md#t072) | reaction_or_regular_process_stage | RiskTreatmentAction | EvidenceRequest | CloseEvidenceRequestActionCompletionAuthorization |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/evidence-request.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
