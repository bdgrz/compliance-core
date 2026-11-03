# ArtifactDisposition

- Classification: `business_aggregate`
- Source area: `Retention` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / artifact-dispositions / DispositionId, with exact approved content and execution attempt identity. Selected mutations use CommandId.

**Invariant:** Only an approved exact source with elapsed retention/no active holds or reliance may start execution; one outstanding attempt; disposed result is terminal and yields tombstone.

**Additional decision rule:** Approval requires an Org Admin actor and a bounded rationale recorded as an immutable decision; approvals may be batched across artifacts, and nothing is disposed of automatically. Disposition leaves a tombstone carrying content hash, size, retention basis and the approving decision. An active hold on a held artifact blocks disposition during client offboarding, and all other artifacts follow the retention schedule once the ClientExport is delivered.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveArtifactDisposition](../commands/artifact-disposition.md#approveartifactdisposition) | command | CommandId |
| [ExecuteArtifactDisposition](../commands/artifact-disposition.md#executeartifactdisposition) | command | CommandId |
| [RecordArtifactDispositionResult](../commands/artifact-disposition.md#recordartifactdispositionresult) | command | CommandId |
| [StopArtifactDisposalForHold](../commands/artifact-disposition.md#stopartifactdisposalforhold) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactDispositionApproved](../events/artifact-disposition.md#artifactdispositionapproved) | bdgrz.retention.artifact-disposition-approved | 1 |
| [ArtifactDispositionExecutionRequested](../events/artifact-disposition.md#artifactdispositionexecutionrequested) | bdgrz.retention.artifact-disposition-execution-requested | 1 |
| [ArtifactDispositionResultRecorded](../events/artifact-disposition.md#artifactdispositionresultrecorded) | bdgrz.retention.artifact-disposition-result-recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X006](../reactors/artifact-legal-hold-reactor.md#x006) | planned_reaction | ArtifactLegalHold | ArtifactDisposition | StopArtifactDisposalForHold |

## Product rules

- **D16-07** — An evidence artifact is eligible for disposition only when its retention period has elapsed and it has no active hold. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-08** — Disposition of evidence requires Org Admin approval recorded as an immutable decision, may be batched, and leaves a tombstone with content hash, size, retention basis, and the decision; nothing is disposed of automatically. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-15** — When a client offboards, active holds block disposition of held artifacts; everything else follows the retention schedule after the ClientExport is delivered. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [artifact-disposition-review-2026-10-29.md](../../product/definitive-aggregate-choreography/artifact-disposition-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/artifact-disposition.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
