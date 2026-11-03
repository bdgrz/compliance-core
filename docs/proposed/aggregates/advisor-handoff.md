# AdvisorHandoff

- Classification: `business_aggregate`
- Source area: `AdvisorCollaboration` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped handoffId; `HandoffId` on `CreateAdvisorHandoff` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Small selection, readiness, revocation/closure and admission-generation authority.

**Additional decision rule:** A handoff selects a bounded set of exact draft record versions that only the Compliance Lead may mark ready_for_review; guests and advisors are admitted only against a ready, unrevoked and unclosed handoff. Closing or revoking ends advisor and guest access to its records immediately while history is retained.

**Must preserve:** Feedback records exact selected source version and admitted generation; later selection makes it historical.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CloseAdvisorHandoff](../commands/advisor-handoff.md#closeadvisorhandoff) | http | CommandId |
| [CreateAdvisorHandoff](../commands/advisor-handoff.md#createadvisorhandoff) | http | HandoffId |
| [MarkAdvisorHandoffReadyForReview](../commands/advisor-handoff.md#markadvisorhandoffreadyforreview) | http | CommandId |
| [SubmitAdvisorFeedback](../commands/advisor-handoff.md#submitadvisorfeedback) | http | FeedbackId |
| [ReviseAdvisorHandoffSelection](../commands/advisor-handoff.md#reviseadvisorhandoffselection) | http | CommandId |
| [RevokeAdvisorHandoff](../commands/advisor-handoff.md#revokeadvisorhandoff) | http | CommandId |
| [AuthorizeAdvisorArtifactDownload](../commands/advisor-handoff.md#authorizeadvisorartifactdownload) | receiver | CommandId |
| [CloseAdvisorArtifactDownloadAuthorization](../commands/advisor-handoff.md#closeadvisorartifactdownloadauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AdvisorArtifactDownloadAuthorized](../events/advisor-handoff.md#advisorartifactdownloadauthorized) | bdgrz.advisor-handoff.advisor-artifact-download-use-authorized | 1 |
| [AdvisorArtifactDownloadAuthorizationClosed](../events/advisor-handoff.md#advisorartifactdownloadauthorizationclosed) | bdgrz.advisor-handoff.advisor-artifact-download-use-settled | 1 |
| [AdvisorFeedbackAdmitted](../events/advisor-handoff.md#advisorfeedbackadmitted) | bdgrz.advisor-handoff.advisor-feedback-admitted | 1 |
| [AdvisorHandoffClosed](../events/advisor-handoff.md#advisorhandoffclosed) | bdgrz.advisor-handoff.closed | 2 |
| [AdvisorHandoffCreated](../events/advisor-handoff.md#advisorhandoffcreated) | bdgrz.advisor-handoff.created | 2 |
| [AdvisorHandoffOpenedForReview](../events/advisor-handoff.md#advisorhandoffopenedforreview) | bdgrz.advisor-handoff.readied-for-review | 1 |
| [AdvisorHandoffRevoked](../events/advisor-handoff.md#advisorhandoffrevoked) | bdgrz.advisor-handoff.revoked | 2 |
| [AdvisorHandoffSelectionRevised](../events/advisor-handoff.md#advisorhandoffselectionrevised) | bdgrz.advisor-handoff.selection-revised | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T013](../reactors/preserve-advisor-feedback-after-handoff-reactor.md#t013) | reaction_or_regular_process_stage | AdvisorHandoff | AdvisorFeedbackThread | RecordAdvisorHandoffFeedback |
| [T099](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t099) | reaction_or_regular_process_stage | EvidenceArtifact | AdvisorHandoff | AuthorizeAdvisorArtifactDownload |
| [T102](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t102) | reaction_or_regular_process_stage | ArtifactDownloadGrant | AdvisorHandoff | CloseAdvisorArtifactDownloadAuthorization |

## Product rules

- **D14-06** — An AdvisorHandoff is a bounded selection of exact draft record versions that the Compliance Lead marks ready_for_review; advisors and guests see only records in an open handoff plus already-approved records. Decision: [m0-d14-advisor-collaboration.md](../decisions/m0-d14-advisor-collaboration.md).
- **D14-07** — Closing or revoking a handoff removes advisor and guest access to its records while history stays. Decision: [m0-d14-advisor-collaboration.md](../decisions/m0-d14-advisor-collaboration.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/advisor-handoff.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
