# ArtifactRetentionBasis

- Classification: `business_aggregate`
- Source area: `Retention` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped artifactContentId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One supported retention basis for exact content identity.

**Additional decision rule:** Default retention is seven years after the end of the period the evidence supports (RetainsThrough = period end + 7 years); for point-in-time evidence the period end is its as-of date. A basis whose end date or kind is inconsistent with its source is refused.

**Must preserve:** Retention is not itself disposal authority; no release by timeout or missing read.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordArtifactRetentionBasis](../commands/artifact-retention-basis.md#recordartifactretentionbasis) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactRetentionBasisRecorded](../events/artifact-retention-basis.md#artifactretentionbasisrecorded) | bdgrz.artifact_retention.basis_recorded | 1 |
| [ArtifactRetentionPeriodSet](../events/artifact-retention-basis.md#artifactretentionperiodset) | bdgrz.artifact_retention.bound | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D16-05** — Default evidence retention is 7 years after the end of the period the evidence supports; for point-in-time evidence the period end is its as-of date. Decision: [m0-d16-evidence-handling.md](../decisions/m0-d16-evidence-handling.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/artifact-retention-basis.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
