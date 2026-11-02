# ArtifactDownloadProcess

- Root source events: EvidenceArtifactDownloadRequested
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| acquire | AuthorizeAdvisorArtifactDownloadUse, AuthorizeArtifactHandlingDownloadUse | Canonical order; the handoff use exists only when a handoff is selected. |
| issue | IssueProtectedArtifactDownloadGrant | Encrypted result verified before the native fact. |
| settle | SettleAdvisorArtifactDownloadUse, SettleArtifactHandlingDownloadUse, SettleArtifactDownloadAvailabilityUse | [Placeholder: no stage-specific rule recorded] |

Abort commands: CancelProtectedArtifactDownloadGrant

Complete selected process definition: [JSON](../data/processes/artifact-download-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
