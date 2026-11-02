# ArtifactEngagementHold

- Classification: `business_aggregate`
- Source area: `Retention` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / artifact-engagement-holds / HoldId, bound to exact artifact, engagement and report issuance. Selected mutations use CommandId.

**Invariant:** One preservation hold cannot release before one year after its exact report issuance; report identity and release decision are immutable. Release also requires the attributed decision that the engagement record package was delivered into the attest team's audit software; until that delivery fact exists the hold blocks disposition and offboarding cannot complete (M0-D27 D27-05)

**Additional decision rule:** A hold is either engagement kind, applying until the report is issued plus one year, or legal kind, applying until an Org Admin releases it with no end date. An engagement hold stays active until one year after report issuance.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [PlaceArtifactEngagementHold](../commands/artifact-engagement-hold.md#placeartifactengagementhold) | creation | HoldId |
| [RecordEngagementReportIssued](../commands/artifact-engagement-hold.md#recordengagementreportissued) | command | CommandId |
| [ReleaseArtifactEngagementHold](../commands/artifact-engagement-hold.md#releaseartifactengagementhold) | command | CommandId |
| [RecordEngagementPackageDelivered](../commands/artifact-engagement-hold.md#recordengagementpackagedelivered) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactEngagementHoldPlaced](../events/artifact-engagement-hold.md#artifactengagementholdplaced) | bdgrz.retention.artifact-engagement-hold-placed | 1 |
| [ArtifactEngagementHoldReleased](../events/artifact-engagement-hold.md#artifactengagementholdreleased) | bdgrz.retention.artifact-engagement-hold-released | 1 |
| [EngagementReportIssued](../events/artifact-engagement-hold.md#engagementreportissued) | bdgrz.retention.engagement-report-issued | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X007](../reactors/client-export-delivery-reactor.md#x007) | planned_reaction | ClientExport | ArtifactEngagementHold | RecordEngagementPackageDelivered |

## Product rules

- **D16-06** — An EvidenceHold is either engagement kind, applying until the report is issued plus 1 year, or legal kind, applying until an Org Admin releases it with no end date. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D27-05** — An attributed decision must record that the engagement record package was delivered into the attest team's audit software; the engagement hold blocks disposition until that delivery decision exists, so offboarding cannot complete without it. Source: [m0-d27-attest-scope.md](../../product/decisions/m0-d27-attest-scope.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [artifact-engagement-hold-review-2026-10-29.md](../../product/definitive-aggregate-choreography/artifact-engagement-hold-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/artifact-engagement-hold.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
