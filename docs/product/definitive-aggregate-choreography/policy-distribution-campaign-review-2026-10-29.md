# PolicyDistributionCampaign aggregate review, 2026-10-29

Scope: domain-event/aggregate stage only. Handlers, reactors and projectors are untouched. Evidence is written, not executed; build and run are pending.

## Boundary decision

**Keep together (retain).** `PolicyDistributionCampaign` (stream `policy-distribution-campaigns/{campaignId}` under the tenant) owns the campaign's exact material, frozen launch audience, attributed audience reconciliations, admission of participant results on the open generation, and the sealed closure (accept, cancel, complete). Per-person results stay on `PolicyCampaignParticipant` (already split out; the campaign admits each result against its frozen audience, Jev keep_per_child_admission 1.00).

- Identity: tenant plus `campaignId`. The stream derives from the pair only; the selected logical id (command id) is acceptance bookkeeping and cannot fork the stream.
- Invariant: a campaign launches once with its frozen audience; results and reconciliations are admitted only while it is open, at the next logical revision; a closure seals admission, can be canceled (new generation) or completed exactly once with the frozen totals.
- Exclusions: roster evaluation and joiner/mover/leaver derivation (`RosterAudience`), per-person acknowledgement, waiver and completion rules (`CampaignRules`, participant owner), authorization, work/notification creation, the open `OPEN_CampaignNaturalKey` question (second open campaign for the same version and roster; unsettled, no owner added).
- Splitting closure from admission would let a result race the seal; both decide on the same stream, so no split.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Launch (own stream, logical id = campaign id) | launched, revision 1 | `PolicyCampaignLaunched` + `PolicyCampaignAudienceFrozen` |
| none | Launch (foreign stream or logical id) | validation refusal | none |
| launched | Launch | conflict | none |
| none | Admit / Reconcile / Accept closure | not found | none |
| open | Admit / Reconcile / Accept closure (stale expected revision) | stale refusal | none |
| open | Admit / Reconcile / Accept closure (no expected revision) | validation refusal | none |
| open | Admit result | revision +1 | `PolicyParticipantResultAdmitted` |
| open | Reconcile (new roster snapshot) | revision +1, reconciliation +1 | `PolicyCampaignAudienceAmended` |
| open | Reconcile (already reconciled snapshot / not the selected snapshot) | conflict | none |
| open | Accept closure | sealed, revision +1 | `PolicyCampaignClosureAccepted` |
| sealed or closed | Admit / Reconcile / Accept closure | conflict | none |
| sealed | Cancel closure (exact pending) | reopened, generation +1, revision +1 | `PolicyCampaignClosureCanceled` |
| sealed | Complete closure (exact pending intent) | closed, revision +1 | `PolicyCampaignClosed` |
| no pending | Cancel / Complete closure | conflict | none |

## Changes (real gaps)

None to the source. The aggregate already carries identity, replay guards (tenant, campaign, operation owner, revision, generation, pending-closure match), the native replay cut for appends, and bounded-history tests. The only gap was the absence of a focused owner-level test for the lifecycle with no host; added `PolicyDistributionCampaignLifecycleTests`.

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveStreamFromTenantAndCampaignGivenAnySelectedLogicalId` |
| Valid launch / event payload | `ShouldLaunchAndFreezeAtRevisionOneGivenUnlaunchedPolicyCampaign`, `ShouldOmitAcknowledgementTextGivenTrainingCampaign` |
| Invalid decision appends nothing | `ShouldRefuseLaunchGivenOperationWhoseLogicalIdIsNotTheCampaign`, `ShouldRefuseAdmissionGivenUnlaunchedCampaign`, `ShouldRefuseReconciliationGivenUnlaunchedCampaign`, `ShouldRefuseRootOperationGivenNoExpectedRevision`, `ShouldRefuseCancelAndCompleteGivenNoPendingClosure` |
| Changed intent / second launch | `ShouldRefuseLaunchAndAppendNothingGivenLaunchedCampaign` |
| Stale revision | `ShouldRefuseAdmissionGivenStaleExpectedRevision`, `ShouldAcceptAdmissionPreparationGivenCurrentRevision` |
| Replay of valid history | `ShouldRestoreOpenCampaignGivenValidLaunchHistory` |
| Result admission, sealed/closed refusal, exact retry, stale revision at host level | `CampaignAdmissionTests` |
| Audience freeze, amendment, closing totals, large audience replay | `PolicyDistributionCampaignTests` |
| Native cut, selected roster absence, original terminal closure after later generations | `PolicyCampaignNativeHistoryTests` |
| Bounded history (many reconciliations, admissions, canceled closures) | `PolicyCampaignNativeBoundaryTests` |
| Exact retry of committed fact after later facts | `CampaignAcceptedRetryTests`, `CampaignAdmissionTests` (acceptance receipts) |
| Competing decisions | framework OCC on append plus `PolicyCampaignReplayCut` requirement (`CampaignLaunchPreparationRaceTests`, `CampaignReceiverPreparationRaceTests`) |

Malformed-history tests were not added, by user preference. Owner-level post-launch commands use a test-confirmed replay cut (see Review before build); cancel and complete are covered at host level.

## Event contract status

- `PolicyCampaignLaunched`, `PolicyCampaignAudienceFrozen`, `PolicyCampaignAudienceAmended`, `PolicyParticipantResultAdmitted`, `PolicyCampaignClosureAccepted`, `PolicyCampaignClosureCanceled`, `PolicyCampaignClosed`: business facts; no add, rename, remove or payload change requested.
- `PolicyCampaignLaunched` carries `Actor` and `ActorMemberId` naming the same member (enforced on replay); a possible redundancy left to the schema owner.

## Later-stage follow-ups (not aggregate gaps)

- `OPEN_CampaignNaturalKey`: if a duplicate open campaign for the same version and roster is declared an error, an active-campaign claim owner (Jev 0.72) is a later design step.
- Handlers, reactors and projectors for notifications, work and campaign views.

## Evidence

- Jev: no request this round; decisions follow the catalog entry and existing source.
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~PolicyDistribution"`, then the unit suite.
- RED: none captured; not claimed.
- Residual risk: the new test file is uncompiled; it assumes `Result.Error` kinds NotFound (unlaunched) and Conflict, and that `NativeFacts.Root` actors round-trip equal through JSON for the launch replay guard.

## Review before build

Independent review, 2026-10-29. Nothing compiled or run.

### Findings and fixes

1. Coverage gap (fixed): the implementer's lifecycle tests covered only refusals and launch; the core invariant (admit at next revision, closure seals admission, reconciliation against the selected snapshot) had no owner-level test. The review file claimed owner-level post-launch tests were impossible without the aggregate reader. They are possible: the test assembly sees the internal `ReplayCut`, and `Finalized(owner)` confirms the cut with `Confirm(owner, owner.ReplayCut.Digest)`, which is what the history reader does after verifying the prefix. Added to `PolicyDistributionCampaignLifecycleTests`: `ShouldAdmitResultAtNextRevisionGivenOpenCampaignAndCurrentExpectedRevision`, `ShouldSealAdmissionGivenAcceptedClosure` (also asserts a late `AdmitResult` is refused and admission count stays 0), `ShouldAmendAudienceGivenOpenCampaignAndSelectedRosterSnapshot`, `ShouldRefuseReconciliationAndAppendNothingGivenRosterSnapshotThatIsNotTheSelectedOne`, `ShouldRefuseAdmissionAndAppendNothingGivenStaleExpectedRevisionOnAdmitResult`, `ShouldRefuseAppendGivenOwnerWhoseSelectedLogicalIdIsNotTheOperation`. One command per finalized owner (each raise invalidates the cut), so cancel and complete with a pending closure stay covered at host level (`PolicyCampaignNativeHistoryTests`, `CampaignAcceptedRetryTests`).
2. Weak assertion (fixed): the stale-revision theory asserted only failure; it now asserts `RequestErrorKind.Conflict`.
3. Implementation: no defect found. Every admission path goes through `PrepareOpenAdmission` (launched, not closed, not sealed, expected revision equals current) and the replay cut; replay handlers re-check tenant, campaign, owner stream, revision, generation and pending-closure match. Exact retries of committed facts are resolved by the host through acceptance receipts, not by the owner (a launch retry on the owner is a conflict by design, covered at host level). No payload field duplicates metadata except the item below.
4. Compile read: signatures of `LaunchCampaign`, `Reconcile`, `AdmitResult`, `AcceptClosure`, `CompleteClosure`, `Result.Error`, `NativeFacts.Root/Raised/Replay` and event properties were checked against source. `ReplayCut` and `PolicyCampaignReplayCut` are internal; `Bdgrz.Compliance.Tests` has `InternalsVisibleTo`. Unconfirmed: that `Confirm` is accepted for a scenario-hydrated owner whose `CommittedStreamPosition` is 0 (it only snapshots the current values, so it should), and whether the style analyzers flag the unused `AccessControl.Models` and `Operations.Models` usings (the existing test files carry many unused usings, so it is probably not enforced).

### Jev (jev-1.14.0, advisory)

Request and response: `implementation-jev/policydistributioncampaign-test-impl-review-2026-10-29-request.json` and `-response.json`.

| Question | Answer | Probability |
|---|---|---|
| Seal test proves admission sealed after closure accepted | true | 0.71 (moderate; test strengthened with a late AdmitResult refusal) |
| Owner hydrated for another logical id, correct revision | retryable conflict, no event | 1.00 |
| Reconcile with non-selected snapshot | conflict, no event | 1.00 |
| Launched `Actor` and `ActorMemberId` duplicate one fact (premise) | true | 0.91 |
| Disposition of that duplication | remove_actor_member_id (keep_both 0.24) | 0.75, confidence 0.62 |

### Discrepancy disposition

- `PolicyCampaignLaunched` carries both `Actor` and `ActorMemberId`. Jev leans to removing `ActorMemberId` (0.75 vs 0.24, confidence 0.62). It is a schema and event-contract change in shared files, so it is not applied. Reported as a remaining item for the schema owner; replay currently enforces that both name the same member. `OwnerMemberId` would derive from `Actor.Id`. Not treated as decisive enough to change without the schema owner.
- `OPEN_CampaignNaturalKey` stays unresolved: it is recorded in the catalog as leaning error 0.65 with a claim owner at 0.72; no source for the owner decision exists.
- No event is missing from or extra to the catalog; no name conflicts found.

### Remaining risks

- New tests are uncompiled and unexecuted. Cancel and complete are not covered at owner level.
- Status stays partial with open item `OPEN_CampaignNaturalKey`.


## Coordinator build and test evidence (2026-10-29, wave 4)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7392 passed, 5 failed of 7397 (the three CorrectiveActionLifecycleTests failures from the first run were fixed and re-run green).. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
