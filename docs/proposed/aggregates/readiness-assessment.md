# ReadinessAssessment

- Classification: `business_aggregate`
- Source area: `Readiness` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped assessmentId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One immutable assessment source/rule revision and findings.

**Additional decision rule:** Each assessment records its rule version, source record versions, completeness, as-of time, output and derived gaps; rule definitions and input selection are owned solely by R1-08, so the assessment never selects inputs or edits rules itself.

**Must preserve:** No projected latest assessment or gap list can authorize entry; all exceptions/required plans acknowledged.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ComputeReadinessAssessment](../commands/readiness-assessment.md#computereadinessassessment) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ReadinessAssessmentRecorded](../events/readiness-assessment.md#readinessassessmentrecorded) | bdgrz.readiness.assessment.recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T039](../reactors/readiness-run-reactor.md#t039) | reaction_or_regular_process_stage | ReadinessAssessment | ProgramReadiness | AdoptReadinessAssessment |
| [T087](../reactors/readiness-run-reactor.md#t087) | reaction_or_regular_process_stage | ProgramReadiness | ReadinessAssessment | ComputeReadinessAssessment |

## Product rules

- **D23-09** — Readiness rule definitions, input selection, and assessment calculation are owned solely by R1-08; each ReadinessAssessment records rule version, source record versions, completeness, as-of time, output, and derived gaps. Source: [m0-d23-assurance-vocabulary.md](../../product/decisions/m0-d23-assurance-vocabulary.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [readiness-assessment-review-2026-10-29.md](../../product/definitive-aggregate-choreography/readiness-assessment-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/readiness-assessment.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
