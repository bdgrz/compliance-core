# EvidenceArtifact aggregate review

## Identity, invariant and boundary

- **Identity:** tenant plus ArtifactId via the existing `EvidenceStreams.Address(tenantId, id)` key. ArtifactId identifies registration; no caller key forks the stream.
- **Named invariant:** immutable content stays unavailable until a clean inspection; only a malware quarantine can be released, by an attributed member decision after a clean rescan; secret-detected or invalid rejection is final. Downloads are issued only for available content bound to the registered hash and length.
- **Exclusions:** purge/tombstone and redaction (`EvidenceRedaction`, retention), download grant tickets and receiving identity (`ArtifactDownloadGrant`), metadata views and rosters (projections), blob storage and scanning (services).
- **Decision:** keep as one aggregate; registration, inspection, release and download-revision are one lifecycle decided atomically.

## Lifecycle

| State | Command | Event | Refusal (no event appended) |
|---|---|---|---|
| none | `Register` | `EvidenceArtifactRegistered` | invalid metadata (Validation); different content on retry (Conflict) |
| PendingInspection | `RecordInspection` | `EvidenceArtifactInspected` | unregistered (Missing); time before registration or unknown outcome (InvalidContent) |
| Available / Quarantined / Rejected | `RecordInspection` same outcome and instant | none (no-op, even after release or download) | different outcome or instant (StateConflict) |
| Available | `RecordDownload` at current revision | `EvidenceArtifactDownloadIssued` | not available (StateConflict); stale revision; wrong hash/length, purpose or actor (InvalidContent) |
| Quarantined (malware) | `ReleaseQuarantine` | `EvidenceArtifactQuarantineReleased` (-> Available) | rejected/other state (StateConflict); stale revision; non-member actor, blank rationale, rescan before inspection (InvalidContent) |
| Released | `ReleaseQuarantine` exact retry | none | different request or intent (StateConflict) |

## Changes (real gaps)

- `RecordInspection` accepted any timestamp and treated every second inspection as a conflict. It now refuses a default timestamp or one before registration, and treats the identical committed inspection (same outcome and instant) as a no-op, including after later release or download facts.
- `ReleaseQuarantine` now requires the decider to be a member with a UUID id (as downloads already did) and a rescan not earlier than the inspection.

## Test-to-criterion table

| Criterion | Tests |
|---|---|
| Stable identity | `ShouldAddressStreamByTenantAndArtifactGivenIdentity` |
| Success path | registration, inspection theory, release, `ShouldIssueDownloadAndAdvanceRevisionGivenAvailableArtifact` |
| Invalid decision appends nothing | invalid metadata theory, `ShouldRefuseInspectionGivenUnregisteredArtifact`, `ShouldAppendNothingGiven...` (inspection before registration, unknown outcome, other-content/blank-purpose download, rescan before inspection, non-member release), `ShouldNeverReleaseGivenSecretDetected`, `ShouldRefuseDownloadGivenArtifactThatIsNotAvailable` |
| No-op / exact retry after later facts | `ShouldReplayIdenticalAndRejectChangedRegistrationGivenRegisteredArtifact`, `ShouldTreatSameInspectionAsNoOpGivenCommittedInspectionAndLaterRelease`, `ShouldTreatSameReleaseAsNoOpGivenCommittedReleaseAndLaterDownload` |
| Changed intent | registration changed content, `ShouldRefuseSecondInspectionGivenAlreadyInspected`, `ShouldRefuseChangedReleaseGivenCommittedRelease` |
| Stale revision / competing | `ShouldRefuseStaleRevisionGivenCompetingDownload`, `ShouldRefuseStaleRevisionGivenQuarantineRelease` |
| Replay | `ShouldRestoreAvailableArtifactGivenValidHistory` |
| Bounded history | state, one inspection, one release and a revision counter only |

## Event contract status

`EvidenceArtifactRegistered`, `Inspected`, `QuarantineReleased` and `DownloadIssued` carry business facts only; no metadata duplication. No event change needed.

## Later-stage follow-ups (not aggregate gaps)

- Exact retry of a committed download request after later downloads is decided by the artifact-qualified `ArtifactDownloadGrant` receiving identity (catalog), not by this aggregate; the aggregate refuses a stale revision.
- Handlers that supply inspection instants and release actors must pass member actors with UUID ids.
- Secret rejection purge/tombstone belongs to the retention/redaction stage.

## Evidence

Tests written, not executed; build and run pending. Source changes were written with their tests (RED not run). Nothing was built, formatted or committed. Risk: existing handler/process tests that inspect with a timestamp earlier than registration or release with a non-UUID member id would now be refused.

## Review before build

Independent review, nothing built or run.

### Findings and fixes
- **Release retry too loose (fixed):** the committed-release retry compared request id, rationale, decider and rescan state but not `RescannedAt` or `DecidedAt`, so a retry with different instants was acknowledged as a no-op. It now compares both; a changed instant is a StateConflict.
- **Weak changed-release test (fixed):** `ShouldRefuseChangedReleaseGivenCommittedRelease` built a fresh `Admin()` per call, so the decider differed in every case and the rationale and request cases could not isolate their own change. It now reuses one decider, adds changed-rescan-instant and changed-decider cases, and asserts the pending event count is unchanged.
- **Weak no-event assertions (fixed):** the inspection-before-registration and unknown-outcome tests asserted `Assert.Single` of pending events, which is satisfied by the registration event. They now assert no `EvidenceArtifactInspected` is pending.
- **Compile risk (fixed):** `PendingEvents.Count()` replaced by the `Count` property (CA1829); `PendingEvents.Last()` replaced by `OfType<EvidenceArtifactDownloadIssued>()` with `Assert.Single` (CA1826).
- No event, catalog or payload discrepancy found; no event change needed.

### Jev (jev-1.14.0, advisory)
Request and response: `implementation-jev/evidenceartifact-test-impl-review-2026-10-29-request.json` and `-response.json`.
- Inspection exact retry is a no-op, changed intent conflicts: noop_on_exact_retry 1.00. Applied (already so).
- Release exact retry is a no-op regardless of supplied revision: noop_ignoring_revision 0.98. Applied (already so).
- Release test asserts immutability and no append (Noul): 0.74. Moderate, so the test was strengthened with more cases and the event-count assertion.
- Refusing an inspection before registration is consistent with the rule (Noul): 0.95.

### Remaining risks
- Not compiled or run. Existing handler or process tests that inspect before registration or release with a non-UUID member id would now be refused.
- Download-request exact retry belongs to `ArtifactDownloadGrant`.


## Coordinator build and test evidence (2026-10-29, wave 2)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7145 passed, 5 failed of 7150. The five failures are outside this wave (the same five as before: two AggregateCommandCoverageTests and three WeeklyScheduleOriginalResultTests).
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
