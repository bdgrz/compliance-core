# ProgramReadiness

- Classification: `business_governor`
- Source area: `Readiness` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped tenant / programId: one stream per tenant/program; there is no creation key, the first readiness run opens it, and `AssessmentId` identifies each run while CommandId identifies each decision.

**Invariant:** Compact latest-assessment pointer, decision admission/seal generation, latest management decision and permanent approved Type I entry pointer. Serialize latest-assessment adoption with decision append; keep full assessment/decision content in immutable manifests. Run admission precedes child computation and reserves exactly one pending latest-adoption generation.

**Additional decision rule:** A readiness snapshot is an immutable freeze of exactly one reproducible assessment plus management's Type I entry decision; the owner never recomputes or redefines readiness rules, and monitoring measures cannot change an adopted verdict.

**Must preserve:** No projected latest assessment or gap list can authorize entry; all exceptions/required plans acknowledged.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DecideReadiness](../commands/program-readiness.md#decidereadiness) | http | CommandId |
| [DecideTypeIEntry](../commands/program-readiness.md#decidetypeientry) | http | CommandId |
| [RunReadinessAssessment](../commands/program-readiness.md#runreadinessassessment) | http | AssessmentId |
| [AbortReadinessDecision](../commands/program-readiness.md#abortreadinessdecision) | abort | CommandId |
| [AdoptReadinessAssessment](../commands/program-readiness.md#adoptreadinessassessment) | receiver | CommandId |
| [CompleteReadinessDecision](../commands/program-readiness.md#completereadinessdecision) | receiver | CommandId |
| [CompleteTypeIEntryDecision](../commands/program-readiness.md#completetypeientrydecision) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ReadinessAssessmentAdopted](../events/program-readiness.md#readinessassessmentadopted) | bdgrz.program-readiness.readiness-assessment-adopted | 1 |
| [ReadinessAssessmentRequested](../events/program-readiness.md#readinessassessmentrequested) | bdgrz.program-readiness.readiness-assessment-requested | 1 |
| [ReadinessDecisionAccepted](../events/program-readiness.md#readinessdecisionaccepted) | bdgrz.program-readiness.readiness-decision-accepted | 1 |
| [ReadinessDecisionCanceled](../events/program-readiness.md#readinessdecisioncanceled) | bdgrz.program-readiness.readiness-decision-canceled | 1 |
| [ReadinessDecisionRecorded](../events/program-readiness.md#readinessdecisionrecorded) | bdgrz.readiness.decision.recorded | 1 |
| [TypeIEntryDecisionAccepted](../events/program-readiness.md#typeientrydecisionaccepted) | bdgrz.program-readiness.type-i-entry-decision-accepted | 1 |
| [TypeIEntryDecisionRecorded](../events/program-readiness.md#typeientrydecisionrecorded) | bdgrz.readiness.type_i_entry.decided | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T039](../reactors/reassess-program-readiness-reactor.md#t039) | reaction_or_regular_process_stage | ReadinessAssessment | ProgramReadiness | AdoptReadinessAssessment |
| [T065](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t065) | reaction_or_regular_process_stage | ProgramReadiness | ReadinessGapPlan | AuthorizeReadinessGapPlanReadinessDecision |
| [T066](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t066) | reaction_or_regular_process_stage | ProgramReadiness | ReadinessGapPlan | CloseReadinessGapPlanReadinessDecisionAuthorization |
| [T074](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t074) | reaction_or_regular_process_stage | ProgramReadiness | ProgramReadiness | CompleteReadinessDecision |
| [T075](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t075) | reaction_or_regular_process_stage | ProgramReadiness | ProgramReadiness | CompleteTypeIEntryDecision |
| [T087](../reactors/reassess-program-readiness-reactor.md#t087) | reaction_or_regular_process_stage | ProgramReadiness | ReadinessAssessment | ComputeReadinessAssessment |
| [A009](../reactors/unassigned-domain-choreography.md#a009) | process_abort_binding | ProgramReadiness | ProgramReadiness | AbortReadinessDecision |

## Product rules

- **D23-10** — A ReadinessSnapshot is an immutable freeze of one reproducible readiness assessment calculation plus management's Type I entry decision, and does not redefine readiness rules; monitoring measures cannot silently change a readiness verdict. Decision: [m0-d23-assurance-vocabulary.md](../decisions/m0-d23-assurance-vocabulary.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/program-readiness.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
