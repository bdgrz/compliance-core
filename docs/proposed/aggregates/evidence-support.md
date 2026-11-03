# EvidenceSupport

- Classification: `business_aggregate`
- Source area: `Evidence` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Existing deterministic UUIDv5 of tenant + program/artifact/type/target/revision/period. Selected CommandId binds the exact immutable relationship and actor; the tuple algorithm is unchanged.

**Invariant:** One relationship identity immutably binds exact artifact content/revision, target revision and covered period with attributed sufficiency rationale.

**Additional decision rule:** A validation is invalidated when a new version supersedes the validated record version; the relationship stays bound to the superseded revision and is never rewritten.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [LinkEvidenceSupport](../commands/evidence-support.md#linkevidencesupport) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [EvidenceSupportLinked](../events/evidence-support.md#evidencesupportlinked) | bdgrz.evidence.evidence-support-linked | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/evidence-support.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
