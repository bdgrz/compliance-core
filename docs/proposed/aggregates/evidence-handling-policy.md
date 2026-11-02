# EvidenceHandlingPolicy

- Classification: `business_aggregate`
- Source area: `Retention` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of TenantId + ArtifactId; selected CommandId is bound to the exact artifact/content and policy assignment scope.

**Invariant:** One exact artifact identity has one current reviewed classification, changed only at expected revision with attributed rationale.

**Additional decision rule:** Each artifact carries exactly one handling_class from public, internal, confidential, or restricted; no artifact is unclassified or multiply classified.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssignArtifactHandlingPolicy](../commands/evidence-handling-policy.md#assignartifacthandlingpolicy) | command | CommandId |
| [AuthorizeArtifactHandlingDownloadUse](../commands/evidence-handling-policy.md#authorizeartifacthandlingdownloaduse) | receiver | CommandId |
| [SettleArtifactHandlingDownloadUse](../commands/evidence-handling-policy.md#settleartifacthandlingdownloaduse) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactHandlingDownloadUseAuthorized](../events/evidence-handling-policy.md#artifacthandlingdownloaduseauthorized) | bdgrz.evidence-handling-policy.artifact-handling-download-use-authorized | 1 |
| [ArtifactHandlingDownloadUseSettled](../events/evidence-handling-policy.md#artifacthandlingdownloadusesettled) | bdgrz.evidence-handling-policy.artifact-handling-download-use-settled | 1 |
| [ArtifactHandlingPolicyAssigned](../events/evidence-handling-policy.md#artifacthandlingpolicyassigned) | bdgrz.retention.artifact-handling-policy-assigned | 1 |
| [ArtifactHandlingPolicyRevised](../events/evidence-handling-policy.md#artifacthandlingpolicyrevised) | bdgrz.retention.artifact-handling-policy-revised | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T100](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t100) | reaction_or_regular_process_stage | EvidenceArtifact | EvidenceHandlingPolicy | AuthorizeArtifactHandlingDownloadUse |
| [T103](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t103) | reaction_or_regular_process_stage | ArtifactDownloadGrant | EvidenceHandlingPolicy | SettleArtifactHandlingDownloadUse |

## Product rules

- **D16-01** — Every evidence artifact carries exactly one handling_class from public, internal, confidential, or restricted. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [evidence-handling-policy-review-2026-10-29.md](../../product/definitive-aggregate-choreography/evidence-handling-policy-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/evidence-handling-policy.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
