# AccessPopulationException

- Classification: `business_aggregate`
- Source area: `AccessReviews` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (systemInstanceId, exceptionId); `ExceptionId` on `ExemptMissingAccessPopulation` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One expiring exception or missing-population exception decision.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Independent proposer/reviewer or exact waiver; exception references approved expectation and future expiry.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ExemptMissingAccessPopulation](../commands/access-population-exception.md#exemptmissingaccesspopulation) | http | ExceptionId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessPopulationExceptionRecorded](../events/access-population-exception.md#accesspopulationexceptionrecorded) | bdgrz.access_population.exception_recorded | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [access-population-exception-review-2026-10-29.md](../../product/definitive-aggregate-choreography/access-population-exception-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/access-population-exception.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
