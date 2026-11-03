# AuditorRequest

- Classification: `business_aggregate`
- Source area: `AuditorRequests` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped requestId; `RequestId` on `RecordAuditorRequest` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Admission seal, response-completeness and final resolution decision.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Admission cannot race resolution; exact response selections and dispositions are immutable proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AmendSampleSelection](../commands/auditor-request.md#amendsampleselection) | http | SelectionId |
| [AssignAuditorRequest](../commands/auditor-request.md#assignauditorrequest) | http | CommandId |
| [CreateAuditResponseDraft](../commands/auditor-request.md#createauditresponsedraft) | http | ResponseId |
| [RecordAuditorRequest](../commands/auditor-request.md#recordauditorrequest) | http | RequestId |
| [RecordAuditorRequestComment](../commands/auditor-request.md#recordauditorrequestcomment) | http | CommentId |
| [RecordSampleSelection](../commands/auditor-request.md#recordsampleselection) | http | SelectionId |
| [ReopenAuditorRequest](../commands/auditor-request.md#reopenauditorrequest) | http | CommandId |
| [ResolveAuditorRequest](../commands/auditor-request.md#resolveauditorrequest) | http | CommandId |
| [ReviseAuditorRequest](../commands/auditor-request.md#reviseauditorrequest) | http | CommandId |
| [AbortAuditorRequestResolution](../commands/auditor-request.md#abortauditorrequestresolution) | abort | CommandId |
| [CompleteAuditorRequestResolution](../commands/auditor-request.md#completeauditorrequestresolution) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditorCommentAdmitted](../events/auditor-request.md#auditorcommentadmitted) | bdgrz.auditor-request.auditor-comment-admitted | 1 |
| [AuditorRequestAssigned](../events/auditor-request.md#auditorrequestassigned) | bdgrz.auditorrequests.auditor-request-assigned | 1 |
| [AuditorRequestRecorded](../events/auditor-request.md#auditorrequestrecorded) | bdgrz.auditorrequests.auditor-request-recorded | 1 |
| [AuditorRequestReopened](../events/auditor-request.md#auditorrequestreopened) | bdgrz.auditorrequests.auditor-request-reopened | 1 |
| [AuditorRequestResolutionAccepted](../events/auditor-request.md#auditorrequestresolutionaccepted) | bdgrz.auditor-request.auditor-request-resolution-accepted | 1 |
| [AuditorRequestResolutionCanceled](../events/auditor-request.md#auditorrequestresolutioncanceled) | bdgrz.auditor-request.auditor-request-resolution-canceled | 1 |
| [AuditorRequestResolved](../events/auditor-request.md#auditorrequestresolved) | bdgrz.auditorrequests.auditor-request-resolved | 1 |
| [AuditorRequestRevised](../events/auditor-request.md#auditorrequestrevised) | bdgrz.auditorrequests.auditor-request-revised | 1 |
| [AuditorResponseAdmitted](../events/auditor-request.md#auditorresponseadmitted) | bdgrz.auditor-request.auditor-response-admitted | 1 |
| [AuditorSampleSelectionAdmitted](../events/auditor-request.md#auditorsampleselectionadmitted) | bdgrz.auditor-request.auditor-sample-selection-admitted | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T033](../reactors/preserve-auditor-response-draft-after-request-reactor.md#t033) | reaction_or_regular_process_stage | AuditorRequest | AuditorResponse | CreateAuditorResponseDraft |
| [T034](../reactors/preserve-auditor-comments-after-request-reactor.md#t034) | reaction_or_regular_process_stage | AuditorRequest | AuditorFeedbackThread | RecordAuditorComment |
| [T035](../reactors/auditor-request-resolution-reactor.md#t035) | reaction_or_regular_process_stage | AuditorRequest | AuditorRequest | CompleteAuditorRequestResolution |
| [T061](../reactors/auditor-request-resolution-reactor.md#t061) | reaction_or_regular_process_stage | AuditorRequest | AuditorResponse | AuthorizeAuditorResponseRequestResolution |
| [T062](../reactors/auditor-request-resolution-reactor.md#t062) | reaction_or_regular_process_stage | AuditorRequest | AuditorResponse | CloseAuditorResponseRequestResolutionAuthorization |
| [T105](../reactors/record-auditor-sample-selection-reactor.md#t105) | reaction_or_regular_process_stage | AuditorRequest | AuditorSampleSelection | RecordAuditorSampleSelection |
| [A007](../reactors/unassigned-domain-choreography.md#a007) | process_abort_binding | AuditorRequest | AuditorRequest | AbortAuditorRequestResolution |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/auditor-request.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
