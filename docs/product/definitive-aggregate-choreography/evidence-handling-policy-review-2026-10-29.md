# EvidenceHandlingPolicy review (2026-10-29)

Scope: aggregate stage only (backlog group 36, "Source present; verify"). Verified against the polished `aggregates.json` entry (D16-01).

## Boundary decision

- Identity: `Uuid.CreateVersion5(tenantId, "evidence-handling-policy:{artifactId}")` (`IdFor`); stream `evidence-handling-policies`. No caller key forks a stream. `CommandId` belongs to the handler/idempotency layer and is not an aggregate input.
- Named invariant: one exact artifact identity has one current reviewed classification (public, internal, confidential, restricted), changed only at the expected revision with an attributed rationale.
- Exclusions: inspection (EvidenceArtifact), retention/holds (ArtifactRetention), redaction (EvidenceRedaction), download-use authorization and settlement (catalog events `ArtifactHandlingDownloadUseAuthorized/Settled`, later stage).
- Decision: keep (catalog "narrow"). The current-classification compare-and-set is atomic in this stream; history is an in-state trail bounded at 32 and does not decide anything beyond the latest entry.

## Lifecycle

| State | Command | Result | Event |
|---|---|---|---|
| none | Assign at revision 0 | accepted | ArtifactHandlingPolicyAssigned |
| none | Assign at other revision, invalid source/actor/class/version/rationale | refused, no event | none |
| classified | Assign different class at current revision | accepted | ArtifactHandlingPolicyRevised |
| classified | Assign same class and same rationale/actor at current revision | no-op success | none |
| classified | Assign same class, changed rationale/actor | conflict | none |
| classified | Retry of an earlier committed decision (revision below current) with same class, version, rationale, actor | original result, no-op | none |
| classified | Earlier revision with changed intent, negative or stale revision | conflict | none |
| classified | Changed content identity, regressed source position, 33rd revision | conflict | none |

## Changes

- Gap fixed: the catalog flags that exact-retry comparison "wrongly includes generated AssignedAt". `Assign` compared `AssignedAt`, so a retry with a fresh clock reading conflicted. Retry is now judged on business intent only (class, policy version, trimmed rationale, actor).
- Gap fixed: retry of a committed decision after later decisions returned a stale-revision conflict. A decision at `expectedRevision < Revision` that equals the history entry at that revision now returns success with no event; otherwise it conflicts.
- Gap fixed: a negative expected revision indexed the history path; it now conflicts.
- No other defect found. `ApplyState` re-validates each fact (identity, revision order, append-only history, source continuity).

## Test-to-criterion table

| Criterion | Test |
|---|---|
| Stable identity | `ShouldDeriveStableIdentityGivenTenantAndArtifact` (new) |
| Binding to exact artifact content | `ShouldBindReviewedClassToExactArtifactIdentityGivenAssignment`, `ShouldRejectChangedSourceOrUnsupportedClassGivenAssignment` |
| Invalid decisions append nothing | `ShouldRejectSecretRejectedArtifactGivenAssignment`, `ShouldAppendNothingGivenInvalidAssignment` (new) |
| Exact retry, fresh timestamp | `ShouldTreatRetryWithFreshTimestampAsNoOpGivenSameIntent` (new) |
| Retry after later facts; changed intent | `ShouldReturnOriginalResultGivenRetryOfCommittedDecisionAfterLaterDecision` (new), `ShouldAllowOnlyExactRetryAndReplayClassificationHistoryGivenReclassification` |
| Changed intent at current revision | `ShouldRefuseChangedRationaleGivenSameClassAtCurrentRevision` (new) |
| Stale and competing revision | `ShouldRequireCurrentRevisionAndReasonForReclassificationGivenAssignment`, `ShouldRefuseNegativeRevisionGivenExistingPolicy` (new) |
| Source position regression | `ShouldRefuseRegressedSourcePositionGivenLaterAssignment` (new) |
| Bounded history | `ShouldRefuseFurtherRevisionGivenRetainedVersionLimit` (new) |
| Valid replay, then retry and next decision | `ShouldRestoreStateAndAcceptRetryAndNextDecisionGivenValidHistoryReplay` (new) |

Malformed-history tests were not added, by user preference.

## Event contract status

- `ArtifactHandlingPolicyAssigned` and `ArtifactHandlingPolicyRevised` are retained baseline events and replay. No add, rename or removal is requested.
- Observation, not a request: each payload carries a full `State` snapshot (tenant, artifact, revision, history) that repeats envelope fields. It is retained baseline wire shape; changing it would be a versioned event change for the coordinator to decide.
- Catalog events `ArtifactHandlingDownloadUseAuthorized` and `ArtifactHandlingDownloadUseSettled` target this owner but belong to the later reactor/handler stage; no aggregate behavior for them was built.

## Later-stage follow-ups (not aggregate gaps)

- `AssignArtifactHandlingPolicyHandler`: CommandId binding to artifact/content/expected revision, original-result replay, fresh source snapshot loading.
- `ArtifactHandlingDownloadUseReactor` / `SettleArtifactHandlingDownloadUseReactor` and their receivers, including "conflicting reassignment waits until native grant or cancellation".

## Evidence

- Jev: no request this round; the decisions follow explicit catalog text (AssignedAt flaw, exact retry semantics).
- Tests: written, not executed; build and run pending. Intended: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EvidenceHandlingPolicyTests"`, then the unit suite.
- RED: none captured; not claimed. No host, durable-store or production qualification is claimed.

## Review before build (independent, 2026-10-29)

Nothing was built or run. Source and tests were read as compiler and analyzers would.

### Findings and fixes

- Compile defect (fixed): the test file calls `NativeFacts.Raised` and `NativeFacts.Replay` (internal, namespace `Bdgrz.Compliance.Tests.Testing`) without a using. Added `using Bdgrz.Compliance.Tests.Testing;`.
- Coverage gap (fixed): the branch that acknowledges the identical current decision at the current revision with no new fact had no test. Added `ShouldAcknowledgeWithoutNewFactGivenSameDecisionAtCurrentRevision` (success, revision unchanged, original `AssignedAt`, single pending event).
- Implementation: retry of a committed decision is matched against the history entry at `expectedRevision` and ignores the clock; negative revision, changed intent, source regression and the 32-version bound each refuse without a fact. No wrong or duplicate fact path was found.
- Unconfirmed, pre-existing: the private overload `SameSourceIdentity(View, View)` is unused. It was not added this session and the build is assumed to tolerate it; remove it if IDE0051 fires.
- Observation: a change in the source's own `SourceHandlingClass` or content length conflicts permanently; that follows the catalog's exact-artifact binding and was not changed.

### Jev (model jev-1.14.0; request/response in `implementation-jev/evidencehandlingpolicy-test-impl-review-2026-10-29-*.json`)

| Question | Answer | Probability |
|---|---|---|
| Retry of committed decision after later decision acknowledged without a fact | consistent | 0.99 |
| Identical decision at current revision is a no-op success | noop_success_correct | 0.99 |
| Tests assert behavior and can fail | true | 0.95 |
| Catalog download-use events: disposition | defer_to_reactor_stage | 1.00 |
| Event State snapshot repeats identity/metadata (premise) | true | 0.88 |
| Payload duplication disposition | report_for_versioned_event_change | 0.78 (keep_without_follow_up 0.19; confidence 0.67) |

Dispositions: first four applied or recorded as-is (decisive). Payload duplication is a lead, not decisive on its own; it needs a shared event change, so it is reported to the coordinator and not applied here.

### Remaining risks

- Not compiled or executed.
- Payload `State` snapshot duplicating envelope fields: coordinator decision (versioned event change).
- Handler/reactor follow-ups unchanged.


## Coordinator build and test evidence (2026-10-29, wave 5)

- `dotnet build test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore`: 0 warnings, 0 errors after fixing blind-written test compile errors.
- `dotnet test ... --no-build -c Release --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: 7454 passed, 5 failed of 7459.. The five failures predate this wave: two AggregateCommandCoverageTests (map lacks ArtifactDownloadGrant) and three WeeklyScheduleOriginalResultTests.
- `dotnet format whitespace ... --verify-no-changes --include <changed files>`: clean.
- New tests were written before the build; RED was not observed.
