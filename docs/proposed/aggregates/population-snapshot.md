# PopulationSnapshot

- Classification: `immutable_business_artifact_or_decision`
- Source area: `Snapshots` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / population-snapshots / SnapshotId, with exact immutable root/amendment lineage and canonical population identity. Storage chunk identities remain deterministic storage values, separate from the business snapshot.

**Invariant:** Freeze exactly one canonical immutable population identity, optionally a manifest over ordered immutable chunk identities; amendments have separate roots.

**Additional decision rule:** An exported audit population is one immutable version with its own content hash and a stable row identifier per item; its original version and hash never change.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [FinalizePopulationSnapshot](../commands/population-snapshot.md#finalizepopulationsnapshot) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PopulationSnapshotFinalized](../events/population-snapshot.md#populationsnapshotfinalized) | bdgrz.snapshot.population.frozen | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D17-01** — An exported audit population is one immutable version with its own content hash and a stable row identifier per item; its original version and hash never change. Source: [m0-d17-audit-deliverables.md](../../product/decisions/m0-d17-audit-deliverables.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [population-snapshot-review-2026-10-29.md](../../product/definitive-aggregate-choreography/population-snapshot-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/population-snapshot.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
