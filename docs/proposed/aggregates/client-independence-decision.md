# ClientIndependenceDecision

- Classification: `immutable_business_artifact_or_decision`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped tenant / evaluationId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One immutable client independence preview and exact rule/service-epoch proofs; it never grants production acceptance. The evaluation records the rule-set version, every nonattest service considered, the result (compatible, conditionally_compatible or impaired), the partner evaluation reference when conditional, and the management acknowledgement reference (M0-D26 D26-06; Jev 0.55, moderate)

**Additional decision rule:** The evaluation is refused unless it records the rule-set version, every nonattest service considered, a result of compatible, conditionally_compatible or impaired, the approver and time, a partner evaluation reference when conditionally_compatible, and management's acknowledgement of responsibility for nonattest services.

**Must preserve:** Never reactivate a permanent independence commitment from reevaluation; acceptance binds exact service, draft, acknowledgement and ratified-rule proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [MaterializeClientIndependencePreview](../commands/client-independence-decision.md#materializeclientindependencepreview) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ClientIndependenceEvaluated](../events/client-independence-decision.md#clientindependenceevaluated) | bdgrz.independence.evaluated | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T106](../reactors/client-independence-preview-reactor.md#t106) | reaction_or_regular_process_stage | ClientServiceIndependence | ClientIndependenceDecision | MaterializeClientIndependencePreview |

## Product rules

- **D26-06** — An IndependenceEvaluation records the rule-set version, nonattest services considered, result (compatible, conditionally_compatible, or impaired), partner evaluation when conditional, management's acknowledgement of responsibility for nonattest services, and the approver and time. Source: [m0-d26-independence.md](../../product/decisions/m0-d26-independence.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [client-independence-decision-review-2026-10-29.md](../../product/definitive-aggregate-choreography/client-independence-decision-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/client-independence-decision.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
