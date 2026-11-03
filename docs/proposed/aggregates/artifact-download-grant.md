# ArtifactDownloadGrant

- Classification: `business_aggregate`
- Source area: `[Placeholder: source capability area is not recorded]` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** UUIDv5 namespace ArtifactId, UTF8 artifact-download-grant:v1:{canonical TenantId}:{canonical CommandId}. Receiving root additionally verifies exact artifact/command/original-actor tuple. No generic operation lookup.

**Invariant:** One exact receiving grant owns the authenticated encrypted original artifact ticket/result after EvidenceArtifact first admitted and pinned availability with frozen202. Complete required held source uses and exact issue DecisionAt precede native issued-v2; cancellation fences delayed issuance and terminal proofs settle all uses. Original expiry never renews under the same root identity. Existing artifact-issued v1 decoder remains read-only compatibility.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CancelProtectedArtifactDownloadGrant](../commands/artifact-download-grant.md#cancelprotectedartifactdownloadgrant) | abort | CommandId |
| [IssueProtectedArtifactDownloadGrant](../commands/artifact-download-grant.md#issueprotectedartifactdownloadgrant) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactDownloadGrantCanceled](../events/artifact-download-grant.md#artifactdownloadgrantcanceled) | bdgrz.artifact-download-grant.artifact-download-grant-canceled | 1 |
| [EvidenceArtifactDownloadIssued](../events/artifact-download-grant.md#evidenceartifactdownloadissued) | bdgrz.evidence.evidence-artifact-download-issued | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T101](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t101) | reaction_or_regular_process_stage | EvidenceArtifact | ArtifactDownloadGrant | IssueProtectedArtifactDownloadGrant |
| [T102](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t102) | reaction_or_regular_process_stage | ArtifactDownloadGrant | AdvisorHandoff | CloseAdvisorArtifactDownloadAuthorization |
| [T103](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t103) | reaction_or_regular_process_stage | ArtifactDownloadGrant | EvidenceHandlingPolicy | CloseArtifactHandlingDownloadAuthorization |
| [T104](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t104) | reaction_or_regular_process_stage | ArtifactDownloadGrant | EvidenceArtifact | CloseArtifactDownloadAvailabilityAuthorization |
| [A021](../reactors/unassigned-domain-choreography.md#a021) | process_abort_binding | EvidenceArtifact | ArtifactDownloadGrant | CancelProtectedArtifactDownloadGrant |
| [Q000](../reactors/unassigned-domain-choreography.md#q000) | personal_scoped_query | [Placeholder: value not recorded] | ArtifactDownloadGrant | GetArtifactDownloadGrant |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

- `ARTIFACT_DOWNLOAD` — artifact_first_202_qualified_grant_v2.

## Evidence and complete record

- Baseline source: [Placeholder: baseline source not recorded]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/artifact-download-grant.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
