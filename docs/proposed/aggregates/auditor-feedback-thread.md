# AuditorFeedbackThread

- Classification: `business_aggregate`
- Source area: `AuditorRequests` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (requestId, threadId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Independent comments and responses with stable command identity.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Admission cannot race resolution; exact response selections and dispositions are immutable proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordAuditorComment](../commands/auditor-feedback-thread.md#recordauditorcomment) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditorRequestCommentRecorded](../events/auditor-feedback-thread.md#auditorrequestcommentrecorded) | bdgrz.auditorrequests.auditor-request-comment-recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T034](../reactors/auditor-comment-materialization-reactor.md#t034) | reaction_or_regular_process_stage | AuditorRequest | AuditorFeedbackThread | RecordAuditorComment |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [auditor-feedback-thread-review-2026-10-29.md](../../product/definitive-aggregate-choreography/auditor-feedback-thread-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/auditor-feedback-thread.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
