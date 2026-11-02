# AccessPopulation

- Classification: `business_aggregate`
- Source area: `AccessReviews` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped populationId; `PopulationId` on `OpenAccessPopulation` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Immutable accepted population definition, exact system instance and source revision.

**Additional decision rule:** An accepted population stores only directly asserted assignments as observed, never inherited or effective access; effective access is a derived, versioned calculation per snapshot and is never written as a source fact. Acceptance records the exact system-instance revision, observation time, source (manual attestation until imports exist) and content hash, after which the snapshot is immutable. A provider subject identifier appears at most once per population for a system instance.

**Must preserve:** Classification requires exact accepted population proof and contiguous per-principal decision sequence.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcceptAccessPopulation](../commands/access-population.md#acceptaccesspopulation) | http | CommandId |
| [OpenAccessPopulation](../commands/access-population.md#openaccesspopulation) | http | PopulationId |
| [RecordAccessPopulationFacts](../commands/access-population.md#recordaccesspopulationfacts) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessPopulationAccepted](../events/access-population.md#accesspopulationaccepted) | bdgrz.access_population.accepted | 2 |
| [AccessPopulationFactsRecorded](../events/access-population.md#accesspopulationfactsrecorded) | bdgrz.access_population.facts_recorded | 2 |
| [AccessPopulationOpened](../events/access-population.md#accesspopulationopened) | bdgrz.access_population.opened | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D07-03** — Inherited access is never stored as an AccessAssignment; an assignment records only what the source asserts is granted directly to that principal within a snapshot. Source: [m0-d07-access-review-population.md](../../product/decisions/m0-d07-access-review-population.md).
- **D07-04** — An AccessPopulationSnapshot records the exact system instance version, observation time, source (manual attestation until imports exist), and content hash, and is immutable once accepted. Source: [m0-d07-access-review-population.md](../../product/decisions/m0-d07-access-review-population.md).
- **D07-05** — An account's provider subject identifier is unique per system instance. Source: [m0-d07-access-review-population.md](../../product/decisions/m0-d07-access-review-population.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [access-population-review-2026-10-29.md](../../product/definitive-aggregate-choreography/access-population-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/access-population.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
