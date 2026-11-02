# ClientServiceIndependence

- Classification: `business_governor`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped tenant / client-service-epoch: one stream per tenant (client-service-epoch); there is no creation key, the first NonattestServiceRecorded or evaluation admission opens it, and every command binds CommandId plus the expected service epoch.

**Invariant:** Complete immutable nonattest-service history manifest, current service epoch and acceptance admission barrier. A changed service set advances epoch and makes old acceptance ineligible immediately through authoritative composed reads. Acceptance admission refuses an attest engagement when the firm provided any service classified impairing under the active ratified rule set within the 12 months before the examination period starts (M0-D26 D26-03; Jev 0.62, moderate). When the current client evaluation is conditionally_compatible, acceptance admission also requires a recorded partner evaluation that references that evaluation (M0-D26 D26-04; Jev two-step 0.80, gate owner 0.58 moderate)

**Additional decision rule:** Acceptance admission is refused for an attest engagement when the firm provided any service classified impairing in the active ratified IndependenceRuleSet within the rule set's look-back (12 months) before the examination period starts, evaluated over the full nonattest-service history with no hard-coded service kinds. A client previously served with advisory work goes through the same evaluation, and advisory staff who served the client are excluded from the attest team.

**Must preserve:** Never reactivate a permanent independence commitment from reevaluation; acceptance binds exact service, draft, acknowledgement and ratified-rule proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [EvaluateClientIndependence](../commands/client-service-independence.md#evaluateclientindependence) | http | EvaluationId |
| [RecordNonattestService](../commands/client-service-independence.md#recordnonattestservice) | http | ServiceRecordId |
| [AcquireEngagementServiceEpoch](../commands/client-service-independence.md#acquireengagementserviceepoch) | receiver | CommandId |
| [ReleaseEngagementServiceEpoch](../commands/client-service-independence.md#releaseengagementserviceepoch) | receiver | CommandId |
| [AuthorizeClientServicePersonalDecisionUse](../commands/client-service-independence.md#authorizeclientservicepersonaldecisionuse) | receiver | CommandId |
| [SettleClientServicePersonalDecisionUse](../commands/client-service-independence.md#settleclientservicepersonaldecisionuse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ClientIndependenceEvaluationAdmitted](../events/client-service-independence.md#clientindependenceevaluationadmitted) | bdgrz.client-service-independence.client-independence-evaluation-admitted | 1 |
| [ClientServicePersonalDecisionUseAuthorized](../events/client-service-independence.md#clientservicepersonaldecisionuseauthorized) | bdgrz.client-service-independence.client-service-personal-decision-use-authorized | 1 |
| [ClientServicePersonalDecisionUseSettled](../events/client-service-independence.md#clientservicepersonaldecisionusesettled) | bdgrz.client-service-independence.client-service-personal-decision-use-settled | 1 |
| [EngagementServiceEpochAuthorized](../events/client-service-independence.md#engagementserviceepochauthorized) | bdgrz.client-service-independence.engagement-service-epoch-authorized | 1 |
| [EngagementServiceEpochSettled](../events/client-service-independence.md#engagementserviceepochsettled) | bdgrz.client-service-independence.engagement-service-epoch-settled | 1 |
| [NonattestServiceRecorded](../events/client-service-independence.md#nonattestservicerecorded) | bdgrz.independence.service.recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T004](../reactors/engagement-acceptance-reactor.md#t004) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | AcquireEngagementServiceEpoch |
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | ReleaseEngagementServiceEpoch |
| [T106](../reactors/client-independence-preview-reactor.md#t106) | reaction_or_regular_process_stage | ClientServiceIndependence | ClientIndependenceDecision | MaterializeClientIndependencePreview |
| [T107](../reactors/engagement-personal-decision-reactor.md#t107) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | AuthorizeClientServicePersonalDecisionUse |
| [T111](../reactors/engagement-personal-decision-reactor.md#t111) | reaction_or_regular_process_stage | ServiceEngagementIndependence | ClientServiceIndependence | SettleClientServicePersonalDecisionUse |

## Product rules

- **D26-03** — An attest ServiceEngagement cannot be accepted for a client that received any service classified impairing in the active IndependenceRuleSet from the firm within 12 months before the examination period starts, evaluated against the client's NonattestServiceRecord history with no hard-coded service kinds. Source: [m0-d26-independence.md](../../product/decisions/m0-d26-independence.md).
- **D26-04** — A client whose only nonattest service was a readiness assessment is conditionally_compatible, and acceptance requires a recorded partner evaluation. Source: [m0-d26-independence.md](../../product/decisions/m0-d26-independence.md).
- **D26-08** — A new attest ServiceEngagement for a client previously served with advisory work goes through the same evaluation using the client's full NonattestServiceRecord history, and advisory staff who served the client are excluded from the attest team. Source: [m0-d26-independence.md](../../product/decisions/m0-d26-independence.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [client-service-independence-review-2026-10-29.md](../../product/definitive-aggregate-choreography/client-service-independence-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/client-service-independence.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
