# AuditorResponse

- Classification: `business_aggregate`
- Source area: `AuditorRequests` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (requestId, responseId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One response draft, independent disclosure review, delivery and exact disposition lifecycle. Response admission and disposition remain linked to the parent request through immutable proofs.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Admission cannot race resolution; exact response selections and dispositions are immutable proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DeliverAuditResponse](../commands/auditor-response.md#deliverauditresponse) | http | CommandId |
| [RecordAuditorResponseDisposition](../commands/auditor-response.md#recordauditorresponsedisposition) | http | CommandId |
| [ReviewAuditResponse](../commands/auditor-response.md#reviewauditresponse) | http | CommandId |
| [ReviseAuditResponseDraft](../commands/auditor-response.md#reviseauditresponsedraft) | http | CommandId |
| [SubmitAuditResponse](../commands/auditor-response.md#submitauditresponse) | http | CommandId |
| [CreateAuditorResponseDraft](../commands/auditor-response.md#createauditorresponsedraft) | receiver | CommandId |
| [AuthorizeAuditorResponseRequestResolution](../commands/auditor-response.md#authorizeauditorresponserequestresolution) | receiver | CommandId |
| [CloseAuditorResponseRequestResolutionAuthorization](../commands/auditor-response.md#closeauditorresponserequestresolutionauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditResponseDraftCreated](../events/auditor-response.md#auditresponsedraftcreated) | bdgrz.auditorrequests.audit-response-draft-created | 1 |
| [AuditResponseDraftRevised](../events/auditor-response.md#auditresponsedraftrevised) | bdgrz.auditorrequests.audit-response-draft-revised | 1 |
| [AuditorResponseDelivered](../events/auditor-response.md#auditorresponsedelivered) | bdgrz.auditorrequests.auditor-response-delivered | 1 |
| [AuditorResponseDisclosureReviewed](../events/auditor-response.md#auditorresponsedisclosurereviewed) | bdgrz.auditorrequests.auditor-response-disclosure-reviewed | 1 |
| [AuditorResponseDispositionRecorded](../events/auditor-response.md#auditorresponsedispositionrecorded) | bdgrz.auditorrequests.auditor-response-disposition-recorded | 1 |
| [AuditorResponseRequestResolutionAuthorized](../events/auditor-response.md#auditorresponserequestresolutionauthorized) | bdgrz.auditor-response.auditor-response-request-resolution-use-authorized | 1 |
| [AuditorResponseRequestResolutionAuthorizationClosed](../events/auditor-response.md#auditorresponserequestresolutionauthorizationclosed) | bdgrz.auditor-response.auditor-response-request-resolution-use-settled | 1 |
| [AuditorResponseSubmitted](../events/auditor-response.md#auditorresponsesubmitted) | bdgrz.auditorrequests.auditor-response-submitted | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T033](../reactors/auditor-response-materialization-reactor.md#t033) | reaction_or_regular_process_stage | AuditorRequest | AuditorResponse | CreateAuditorResponseDraft |
| [T061](../reactors/auditor-request-resolution-reactor.md#t061) | reaction_or_regular_process_stage | AuditorRequest | AuditorResponse | AuthorizeAuditorResponseRequestResolution |
| [T062](../reactors/auditor-request-resolution-reactor.md#t062) | reaction_or_regular_process_stage | AuditorRequest | AuditorResponse | CloseAuditorResponseRequestResolutionAuthorization |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [auditor-response-review-2026-10-29.md](../../product/definitive-aggregate-choreography/auditor-response-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/auditor-response.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
