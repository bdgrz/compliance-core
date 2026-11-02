# SignificantChange

- Classification: `business_aggregate`
- Source area: `Changes` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / significant-changes / ChangeId, with immutable ProgramId. ChangeId is the selected immutable creation identity; later selected commands use CommandId and exact changed-version/source bindings.

**Invariant:** One change version may be assessed once; current-version independent description decision and required follow-up eligibility gate advancement.

**Additional decision rule:** A change records a human significance assessment of significant or not_significant with required rationale, assessed_by and assessed_at, and a handling_class from the evidence vocabulary defaulting to confidential. It is significant when it changes the system boundary, a service commitment or system requirement, a key control, or infrastructure hosting in-scope data; the product keeps its own record identity, recorder and dates, and source_system, source_identifier and source_url are optional provenance only.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssessSignificantChange](../commands/significant-change.md#assesssignificantchange) | http | CommandId |
| [LinkImpactReviewWork](../commands/significant-change.md#linkimpactreviewwork) | command | CommandId |
| [RecordSignificantChange](../commands/significant-change.md#recordsignificantchange) | creation | ChangeId |
| [RecordSignificantChangeSystemDescriptionDecision](../commands/significant-change.md#recordsignificantchangesystemdescriptiondecision) | command | CommandId |
| [ReviseSignificantChange](../commands/significant-change.md#revisesignificantchange) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ImpactReviewWorkLinked](../events/significant-change.md#impactreviewworklinked) | bdgrz.changes.impact-review-work-linked | 3 |
| [SignificantChangeImpactAssessed](../events/significant-change.md#significantchangeimpactassessed) | bdgrz.changes.significant-change-impact-assessed | 3 |
| [SignificantChangeRecorded](../events/significant-change.md#significantchangerecorded) | bdgrz.changes.significant-change-recorded | 3 |
| [SignificantChangeRevised](../events/significant-change.md#significantchangerevised) | bdgrz.changes.significant-change-revised | 3 |
| [SignificantChangeSystemDescriptionDecisionRecorded](../events/significant-change.md#significantchangesystemdescriptiondecisionrecorded) | bdgrz.changes.significant-change-system-description-decision-recorded | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S019](../reactors/link-impact-review-work-reactor.md#s019) | business_receiver | SignificantChange | SignificantChange | LinkImpactReviewWork |
| [S020](../reactors/system-description-decision-reactor.md#s020) | business_receiver | SystemDescription | SignificantChange | RecordSignificantChangeSystemDescriptionDecision |

## Product rules

- **D21-01** — An authorized user may record a SignificantChange or ComplianceIncident directly when no source tool exists; the product keeps its own record identity, recorder, dates, assessment, and evidence, and external source_system, source_identifier, and source_url are optional provenance only. Source: [m0-d21-significant-change.md](../../product/decisions/m0-d21-significant-change.md).
- **D21-02** — Significance is a recorded human assessment of significant or not_significant with a required rationale and assessed_by and assessed_at; handling_class uses the evidence vocabulary and defaults to confidential. Source: [m0-d21-significant-change.md](../../product/decisions/m0-d21-significant-change.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [significant-change-review-2026-10-29.md](../../product/definitive-aggregate-choreography/significant-change-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/significant-change.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
