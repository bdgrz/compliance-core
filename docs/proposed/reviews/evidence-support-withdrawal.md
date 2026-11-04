# Evidence support withdrawal

Authority: [Evidence](../contexts/evidence.md), EvidenceSupport command/body and
EV-05. Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

Status: **partial, source-authored and unvalidated**. The owner, command/event,
handler and authoritative reader boundary are prepared. No restore, compilation,
test or formatting command was run. Tests were authored before implementation;
there is no observed behavioral RED or GREEN. Public enablement must wait for
the shared wiring and consumer work below.

## Identity, invariant and lifecycle

The approved owner is named EvidenceSupport, identified by TenantId + SupportId.
The existing authored link CommandId remains its SupportId and its tenant stream
address remains `evidence-support/{support_id}`. Program is an immutable scope
binding within that identity, not a second owner.

One owner decides whether that immutable assertion is still active. A withdrawal
appends a separate attributed, reasoned fact at revision 2. Current and View keep
the original link, exact artifact/hash/target revision/period/rationale and
original actor/time. There is no reactivation or alteration of the link.

WithdrawEvidenceSupport binds a new authored command identity, exact scope and
expected revision. Only its exact accepted command/intent/member retry succeeds
after withdrawal; display changes do not change member identity. Changed intent,
actor or command identity conflicts without another fact. Original Link retries
continue to return the original assertion after withdrawal. Replay requires the
linked predecessor, exact scope/revision and bounded attribution/reason.

WithdrawEvidenceSupportHandler uses existing Portia aggregate execution and
optimistic append. It checks an authenticated current eligible tenant member,
excludes firm-staff membership and requires `evidence_support.withdraw` before
executing the owner or returning an accepted retry. The command carries the
existing client-management mutation and program-scope markers. The coordinator
must grant this permission only to the Compliance Lead built-in role and retain
the current client-management wall for a user with both client membership and
active attest staff authority.

EvidenceSupportReader remains an authoritative aggregate reader. RequireActiveAsync
refuses a withdrawn link; ReadOriginalAsync deliberately retains the immutable
assertion for historical reliance. Both bind tenant and program before returning
content. This source read is not a new cross-owner admission protocol. No
projection, checkpoint, persistence, retry, lease or transaction framework was
introduced.

## Authored behavioral evidence

Tests/contracts were committed first as `6307b091`; this preparation commit
references the subsequent implementation APIs and is not a compilable or observed
RED checkpoint on its own. The final branch needs the shared JSON registrations
before AuthoredIntentDigest can serialize the new command.

| Test class / named behavior | Intended assertion |
| --- | --- |
| EvidenceSupportWithdrawalTests.ShouldKeepOriginalAssertionGivenReasonedWithdrawal | Separate reason/member/time fact, original fact/view unchanged, terminal active state |
| ShouldReturnOriginalLinkWithoutAppendGivenAcceptedLinkRetryAfterWithdrawal | Original frozen link receipt survives withdrawal and later source inputs |
| ShouldKeepFirstWithdrawalGivenExactRetryWithLaterDisplayAndTime | Parsed member identity, including equivalent UUID casing, preserves the first fact |
| ShouldRefuseChangedIntentWithoutAppendGivenWithdrawnSupport | Changed reason/revision/command/actor/tenant/program/support cannot append |
| ShouldRefuseInvalidWithdrawalWithoutFactGivenLinkedSupport | Scope, reason bounds, actor and chronological attribution validation |
| ShouldRefuseStaleRevisionAndMissingLinkWithoutFactGivenWithdrawal | Stale conflict and missing-link refusal leave history unchanged |
| ShouldReplayWithdrawalAndOriginalLinkGivenCompleteHistory | Original view, terminal state and exact retry restored from history |
| ShouldRejectInvalidWithdrawalHistoryGivenLinkedPredecessor | Corrupt scope/revision/intent/attribution is rejected during replay |
| ShouldRejectWithdrawalHistoryGivenMissingLinkOrRepeatedTerminalFact | No missing predecessor or repeated terminal transition |
| WithdrawEvidenceSupportHandlerTests.ShouldWithdrawWithoutChangingHistoricalAssertionGivenAuthorizedLead | Persisted withdrawal blocks active selection while original exact artifact/target binding stays readable |
| ShouldRefuseWithoutFactGivenActorLacksCurrentLeadAuthority | Denied permission, firm staff, absent membership, system and anonymous actors leave the link active |
| ShouldCheckCurrentPermissionBeforeReplayGivenPreviouslyAcceptedWithdrawal | Revoked current permission cannot obtain an accepted retry |
| ShouldNotRevealOriginalOrWithdrawGivenForeignScope | Foreign tenant/program/identity cannot return or withdraw the original |
| ShouldKeepExactRetryAndRejectChangedReasonGivenCommittedWithdrawal | Actual persisted retry appends nothing and changed reason conflicts |
| ShouldCommitOnlyOneWithdrawalGivenCompetingAppends | Existing Portia optimistic concurrency admits one competing fact |

The suite also authors active-link reads and handler stale/invalid-reason refusal.
No pass count, execution clearance or full EV-05 completion is claimed.

## Bounded source and Jev review

The live TypeSafe OpenAPI schema was read from `https://api.typesafe.ai/openapi.json`.
Requests used `jev-latest`, resolved to `jev-1.14.0`, with the key only in the
process environment. Exact local artifacts are
`/tmp/compliance-evidence-support-withdrawal-jev-{request,response,manifest}.json`
and `...-jev-followup-{request,response}.json`; they contain source and questions,
not the key.

Initial signals: exact retry .93, current authority before retry .86, invalid
history refusal .90; original preservation .68 and possible fixture masking .75
required further source review. That review fixed missing ActorReference imports,
replaced the wall clock with a fixed clock, and copied facts before separate
replay scenarios so metadata attachment could not mask the intended terminal
guard. The bounded follow-up judged original fact/view preservation .78, current
authority .87, and partial enablement required .83. Fixture readiness remained
unresolved at .54. These are advisory signals, not executable validation or
clearance. Coordinator compilation and focused execution remain required.

## Exact remaining integration and acceptance gaps

1. `ComplianceCoreJsonContext.cs`: register WithdrawEvidenceSupport and
   EvidenceSupportWithdrawn. EvidenceSupportView is already registered. This is
   required by AuthoredIntentDigest and persisted event round trips.
2. `ComplianceServiceCollectionExtensions.cs`: register
   WithdrawEvidenceSupportHandler and EvidenceSupportReader. The handler requires
   IAggregateExecutor, IAggregateReader, IPermissionAuthorizer and TimeProvider.
   `BuiltInRolePermissions.cs`: grant EvidenceSupportPermissions.Withdraw only to
   the Compliance Lead role. Retain the generic program and client-management
   authorizers; no additional policy framework is needed.
3. `Program.cs` HTTP/MCP composition and shared permission/coverage catalogs are
   coordinator-owned. The command is ICallable and returns Result/Unit. No route
   or tool was published by this branch. Add shared command/event/schema and
   transport coverage before publishing it; there is no new point-read request.
4. `Examinations/Models/EvidenceGovernanceTypeIBaselineSourceContributor.cs`:
   ReadAsync currently casts every support record to EvidenceSupportLinked before
   applying its as-of cut (lines 98-111), and ValidSupportRecord accepts only
   revision-1 linked facts (around line 802). A withdrawal currently invalidates
   that scan even when recorded after a selected cut. Validate the two-event
   lifecycle per support, exclude withdrawal within the selected exact source
   cut for new selection, and preserve already frozen historical reliance.
   New tests must prove before/at/after-cut behavior and a later withdrawal cannot
   poison an earlier cut or frozen source proof. Do not use live projections as
   authority for this decision.
5. `AdvisorCollaboration/Services/AdvisorRecordVersionResolver.cs`,
   RequireEvidenceAsync (around line 170): Current remains the original assertion;
   new handoff selection must also require IsActive. Existing delivered/frozen
   handoff content retains its original source. Add new-withdrawn refusal and
   historical delivery/readability coverage in the owning context.
6. `Applications/Projectors/ApplicationEvidenceSourceReferenceProjector.cs`
   handles Linked only; its FitzApplicationEvidenceSourceReferenceDirectory
   ApplyAsync accepts only Linked (around line 78). Classify its advisory impact
   history versus current active-status contract before changing rows. Any new
   withdrawal/status projection must use the selected Portia.Cassie direction;
   this branch does not expand the legacy Fitz projection or claim that read-side
   acceptance is complete.

The smallest ready delivery boundary is this owner/handler/authoritative-reader
slice. Public withdrawal remains unaccepted until the listed required consumers,
shared wiring, JSON round trips and focused/full executable gates are qualified.
