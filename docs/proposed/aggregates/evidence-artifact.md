# EvidenceArtifact

- Classification: `business_aggregate`
- Source area: `Evidence` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant / existing EvidenceStreams artifact key / ArtifactId, with immutable source identity and verified content binding. ArtifactId identifies registration; selected mutations use CommandId. Download grants use the separately adopted artifact-qualified receiving identity.

**Invariant:** Immutable content remains unavailable until clean inspection; malware alone can be released with attributed clean rescan; secret/invalid rejection is final.

**Additional decision rule:** Rejects an upload with reason secret_detected, purges its content immediately, and keeps only a tombstone (content hash, size, uploader, time, reason); credentials, private keys, access tokens, and session cookies are never stored. A quarantined (malware) artifact cannot be downloaded or shared, and only an Org Admin may release a false positive after rescan or dispose of it. Content hash is verified on restore.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CaptureEvidenceArtifact](../commands/evidence-artifact.md#captureevidenceartifact) | creation | ArtifactId |
| [IssueArtifactDownload](../commands/evidence-artifact.md#issueartifactdownload) | http | CommandId |
| [RecordEvidenceArtifactInspection](../commands/evidence-artifact.md#recordevidenceartifactinspection) | command | CommandId |
| [ReleaseEvidenceArtifactQuarantine](../commands/evidence-artifact.md#releaseevidenceartifactquarantine) | http | CommandId |
| [CloseArtifactDownloadAvailabilityAuthorization](../commands/evidence-artifact.md#closeartifactdownloadavailabilityauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ArtifactDownloadAvailabilityAuthorizationClosed](../events/evidence-artifact.md#artifactdownloadavailabilityauthorizationclosed) | bdgrz.evidence-artifact.artifact-download-availability-use-settled | 1 |
| [EvidenceArtifactDownloadRequested](../events/evidence-artifact.md#evidenceartifactdownloadrequested) | bdgrz.evidence-artifact.evidence-artifact-download-requested | 1 |
| [EvidenceArtifactInspected](../events/evidence-artifact.md#evidenceartifactinspected) | bdgrz.evidence.artifact.inspected | 1 |
| [EvidenceArtifactQuarantineReleased](../events/evidence-artifact.md#evidenceartifactquarantinereleased) | bdgrz.evidence.artifact.quarantine_released | 1 |
| [EvidenceArtifactRegistered](../events/evidence-artifact.md#evidenceartifactregistered) | bdgrz.evidence.artifact.registered | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T099](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t099) | reaction_or_regular_process_stage | EvidenceArtifact | AdvisorHandoff | AuthorizeAdvisorArtifactDownload |
| [T100](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t100) | reaction_or_regular_process_stage | EvidenceArtifact | EvidenceHandlingPolicy | AuthorizeArtifactHandlingDownload |
| [T101](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t101) | reaction_or_regular_process_stage | EvidenceArtifact | ArtifactDownloadGrant | IssueProtectedArtifactDownloadGrant |
| [T104](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t104) | reaction_or_regular_process_stage | ArtifactDownloadGrant | EvidenceArtifact | CloseArtifactDownloadAvailabilityAuthorization |
| [S000](../reactors/evidence-artifact-inspection-effects-reactor.md#s000) | external_storage_reconciliation | EvidenceArtifact | null (recorded) | null (recorded) |
| [A021](../reactors/unassigned-architecture-rows.md#a021) | process_abort_binding | EvidenceArtifact | ArtifactDownloadGrant | CancelProtectedArtifactDownloadGrant |
| [P000](../reactors/unassigned-architecture-rows.md#p000) | personal_source_admission | null (recorded) | EvidenceArtifact | IssueArtifactDownload |

## Product rules

- **D16-02** — Credentials, secrets, private keys, access tokens, and session cookies are never stored; uploads are scanned for them before content becomes available. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-03** — When a secret is detected in an upload, the upload moves to rejected with reason secret_detected, its content is purged immediately, and only a tombstone (content hash, size, uploader, time, reason) is kept. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-04** — A secret-bearing (secret_detected) upload can never be released, quarantined, or made available; the uploader must submit a redacted replacement as a new upload. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-11** — Evidence artifact states are pending_inspection, available, quarantined (reason malware), rejected (reason secret_detected or invalid, content purged), and disposed; every upload must be scanned for malware and secrets before it becomes available. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-12** — A quarantined artifact cannot be downloaded or shared; only an Org Admin may release a malware false positive (a recorded decision after rescan) or dispose of it. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).
- **D16-14** — Evidence content identity (hash) is verified on restore. Source: [m0-d16-evidence-handling.md](../../product/decisions/m0-d16-evidence-handling.md).

## Adopted architecture decisions

- `ARTIFACT_DOWNLOAD` — artifact_first_202_qualified_grant_v2.

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [evidence-artifact-review-2026-10-29.md](../../product/definitive-aggregate-choreography/evidence-artifact-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/evidence-artifact.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
