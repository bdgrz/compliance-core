# ControlEvaluation

- Classification: `business_aggregate`
- Source area: `Evaluations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped tenant / evaluationId; `EvaluationId` on `StartControlEvaluation` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One evaluation including steps, conclusions, review and deviation dispositions. A retest is a new evaluationId using this same type and an immutable predecessor evaluation/version proof.

**Additional decision rule:** Each step records an assertion (design, implementation, evidence_sufficiency), a method (inquiry, inspection, observation, reperformance), typed inspected items and an expected condition. A step result is met, not_met or not_tested with a rationale; per-assertion and overall results are effective, effective_with_exceptions, ineffective or not_tested, and overall effective requires every step of every assertion met. A not_met step requires a deviation classified minor or material: a material deviation makes the result ineffective, creates or links a Finding with a corrective action and requires a retest (a new evaluation naming the deviation); a minor deviation needs a disposition of corrected or accepted_with_waiver and the result is at best effective_with_exceptions. A deviation never closes because a later evaluation passed; closure requires an explicit retest link, and the deviation keeps its procedure, exact inputs, tester and original result.

**Must preserve:** Material deviation source proof, immutable procedure version, reviewer independence, and explicit minor-deviation disposition.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DisposeControlEvaluationDeviation](../commands/control-evaluation.md#disposecontrolevaluationdeviation) | http | CommandId |
| [RecordControlEvaluationStep](../commands/control-evaluation.md#recordcontrolevaluationstep) | http | CommandId |
| [ReviewControlEvaluation](../commands/control-evaluation.md#reviewcontrolevaluation) | http | CommandId |
| [StartControlEvaluation](../commands/control-evaluation.md#startcontrolevaluation) | http | EvaluationId |
| [SubmitControlEvaluation](../commands/control-evaluation.md#submitcontrolevaluation) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ControlEvaluationDeviationDisposed](../events/control-evaluation.md#controlevaluationdeviationdisposed) | bdgrz.control.evaluation.deviation_disposed | 2 |
| [ControlEvaluationReviewed](../events/control-evaluation.md#controlevaluationreviewed) | bdgrz.control.evaluation.reviewed | 2 |
| [ControlEvaluationStarted](../events/control-evaluation.md#controlevaluationstarted) | bdgrz.control.evaluation.started | 2 |
| [ControlEvaluationStepRecorded](../events/control-evaluation.md#controlevaluationsteprecorded) | bdgrz.control.evaluation.step_recorded | 2 |
| [ControlEvaluationSubmitted](../events/control-evaluation.md#controlevaluationsubmitted) | bdgrz.control.evaluation.submitted | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S001](../reactors/control-evaluation-deviation-finding-reactor.md#s001) | business_receiver | ControlEvaluation | FindingAggregate | RaiseEvaluationDeviationFinding |

## Product rules

- **D13-02** — Each procedure step records an assertion (design, implementation, evidence_sufficiency), a method (inquiry, inspection, observation, reperformance), typed inspected items, and an expected condition. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-04** — Each step result is met, not_met, or not_tested with a rationale, and per-assertion and overall results are effective, effective_with_exceptions, ineffective, or not_tested. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-05** — An overall effective result requires every step of every assertion to be met. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-06** — A not_met step creates a ControlEvaluationDeviation that the evaluator classifies as minor or material. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-08** — A minor deviation needs a recorded disposition of corrected or accepted_with_waiver, and the overall result is at best effective_with_exceptions. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-09** — A deviation never closes merely because a later evaluation passed; closure requires an explicit retest link. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-12** — Advisor validated feedback on an evaluation is additive and never counts as the independent review. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D13-07** — A material deviation creates or links a Finding with a corrective action and requires a retest, which is a new evaluation of the same plan version or a successor that explicitly links to the deviation. Decision: [m0-d13-control-evaluation.md](../decisions/m0-d13-control-evaluation.md).
- **D23-02** — A material ControlEvaluationDeviation creates or links to a Finding; the deviation retains the procedure, exact inputs, tester, and original result. Decision: [m0-d23-assurance-vocabulary.md](../decisions/m0-d23-assurance-vocabulary.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/control-evaluation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
