# PolicyCampaignParticipant aggregate review, 2026-10-29

Scope: aggregate/domain event stage. Parent admission, receiver source verification, closure coordination, projections and routes remain separate.

## Identity, invariant and boundary

One tenant and deterministic `(campaignId, personId)` stream owns that person's applied acknowledgement, completion and exception facts. Each fact binds its admitted root; acknowledgement and completion each occur at most once. Keep the participant's results and closure-use hold together because closure must pin the exact applied revision while refusing changes. The campaign owns audience membership, source version, admissions and closure; the receiver verifies the exact committed admission and frozen content. Pending/overdue/excepted/removed views and cadence are broader lifecycle/projection responsibilities under M0-D12, not independently established by this owner's boolean state.

| State | Decision | Outcome |
|---|---|---|
| Fresh or finalized current cut, no closure hold | Apply admitted result | One result fact; applied revision advances |
| Acknowledgement or completion already applied | Apply same result kind again | Conflict; no fact |
| Held by closure | Apply result | Transient conflict; no fact |
| Current revision | Authorize/settle closure use | Hold or fence; applied revision unchanged |
| Exact accepted receiver retry | Resolve original result | No second fact via receiver/native acceptance |

Existing `PolicyCampaignClosureProcessTests` exercise acknowledgement/closure races, held uses, late results, cancellation and recovery; `CampaignAcceptedRetryTests` and `PolicyCampaignNativeHistoryTests` cover selected retry and history verification. They have not been rerun in this phase. A participant may retain multiple exception decisions; long-lived history is not bounded merely by compact aggregate state.

## Gap and focused coverage

The direct result methods checked own stream, selected cut and closure hold but could raise facts rejected by `PolicyCampaignHistory`: an empty policy/requirement identity, zero version, malformed source-child operation or wrong immutable-reference schemas. The owner now validates its identity, child routing, result attribution and the same content-reference schema constraints before emitting; waiver also requires the actual approving member. `PolicyCampaignParticipantOwnerTests` exercises invalid policy/requirement ID and version, child cause/source/kind/actor, intent/result schemas and waiver approver member, with no-fact assertions. Positive tests use a source-shaped operation and the documented reference schemas for all three result types. Exact source event and frozen admitted content remain the receiver's responsibility.

The selected owner event list in `aggregates.json` lists version 1 for `PolicyAcknowledgementRecorded`, `TrainingCompletionRecorded` and `CampaignWaiverApproved`; actual source and `events.json` identify adopted version 2 and describe version 1 as superseded. This is a shared catalog reconciliation discrepancy, not authorization to alter event wire contracts. Independent Jev review is needed for that discrepancy and payload duplication.

## Evidence

Coordinator RED before the fix: `dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PolicyCampaignParticipantOwnerTests|FullyQualifiedName~PracticeIndependenceCommitmentOwnerTests|FullyQualifiedName~ProviderAssuranceGapOwnerTests'` reported 53 failed and 8 passed across the combined selection; invalid participant results were accepted. Independent Jev review and GREEN pending. Durable OCC, recovery and hosted qualification are separate.

Additional reviewer tests exposed malformed native header fields accepted after the initial fix. The same combined filter reported 3 failures and 61 passes: blank episode, malformed digest and default operation acceptance time were accepted. The owner now checks these three fields against the exact `PolicyCampaignHistory.ReadAsync` header requirements before emitting. GREEN remains pending.

## Independent review before build

The receiver verifies the exact admitted parent event and intent, proves the included audience, and builds child provenance and immutable result from the frozen original actor. Its normal child uses this campaign as first owner, the participant as target/affected/revision owner and a nonempty source event as cause, so the new routing and attribution checks match the concrete producer. The history verifier also requires a nonempty operation episode, a 64-character hexadecimal authored digest and a nondefault child `AcceptedAt`. The current append guard does not check those fields; a direct append can therefore be rejected by the mandatory finalized history. Three new focused no-fact cases cover these remaining root-header defects; coordinator RED and fix are pending.

The concrete v2 result events and `events.json` agree that v2 is the occupied adopted wire; v1 in selected `businessOwners[].events` is older superseded metadata. Coordinator should reconcile that shared metadata to the adopted wire without changing event types. The receiver, stateful history verification and source-use cut remain required to prove the exact source, not only these direct input checks.

Jev `jev-1.14.0` is advisory; exact request and response are in `implementation-jev/policycampaignparticipant-test-impl-review-2026-10-29-{request,response}.json`. Live TypeSafe docs were inaccessible, so this uses a retained repository request schema.

| Question | Result | Disposition |
|---|---|---|
| Participant checks match normal child producer | yes 0.39 | Low signal; receiver construction and verified history were checked directly |
| Missing operation header checks can produce history rejection | yes 0.86 | Confirmed source mismatch; tests added, RED/fix pending |
| v1 owner metadata versus adopted v2 wire | reconcile owner metadata 0.97; premise 0.72 | Coordinator shared-file reconciliation; retain occupied wire |
| Repeated identity payload | source resolution 0.57, versioned change 0.36 | Unresolved; no wire change without consumer/migration evidence |

## Coordinator validation (2026-10-29)

Release compilation passed with zero warnings and errors. The related participant, practice-commitment and provider-gap selection passed 124 tests; existing rollover selection passed 18 tests but does not test the missing execution claim. Changed C# files passed whitespace verification. The full non-broker, non-web suite passed 7,951 and failed five: two ArtifactDownloadGrant coverage-map tests and three weekly-schedule original-result tests; the same five failed on hosted develop before wave 2. The full suite is not green. Current producer paths were reviewed; persisted histories, hosted CI, and production behavior remain unqualified.
