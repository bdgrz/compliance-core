# Domain event and aggregate checkpoint

Status: **local work in progress; implementation acceptance is incomplete**. Saved on 2026-10-22 at the user's request to reach a good stopping point. No new aggregate slice is started from this checkpoint.

## Location and recovery

- Working branch: `codex/issue-21-finding-aggregate-boundary`.
- Starting HEAD: `fc7b2543a1c591f8b2cd0787312f6af9e162b3f8`; the pre-existing dirty Finding changes are preserved.
- The local checkpoint commit includes the current code, regressions and decisions. It is not a reviewed feature commit or a deployment claim.
- Recovery snapshot: `/Users/jrepanich/.codex/checkpoints/compliance-domain-20261022T122548Z` contains the pre-checkpoint tracked patch, untracked-file archive, metadata and prepared plans. A verified Git bundle is added after the checkpoint commit.
- Focused delivery task: [#1026](https://github.com/bdgrz/compliance-core/issues/1026), linked under [#1022](https://github.com/bdgrz/compliance-core/issues/1022). The task is In Progress. Broader #21 acceptance remains open.

## Decisions locked

The [decision register](domain-event-aggregate-decisions.json) records actual `jev-1.14.0` requests, answers and evidence for all 409 baseline contracts and 102 roots, plus eight selected additional facts. All 25 close root decisions have a selection. The [decision narrative](domain-event-aggregate-decisions.md) explains the adopted owners and migration rules.

Two follow-up calls are also retained:

- Finding native events use seven explicit schema-v2 contracts with mandatory logical `RequestId`; the seven original v1 contract files remain unchanged. Jev selected this at 0.65 versus 0.35.
- AuditObservation responses and Finding links may reference any exact recorded received version after authoritative fenced-history proof. Jev selected this at 0.99; the historical routing implementation is pending.

Decisions are settled. They do not establish implementation completion.

## Current implementation

- Bounded AuditObservation domain owner and four rich facts: received, corrected, management response and Finding link. Command routes, exact historical proof and source authorization remain pending.
- Finding native v2 facts, selected-request receipts, replay validation, routed two-fact creation intent, source-generation seam and manual/routed writer cutover.
- Finding consumer changes across list/get, control/evaluation/operations sources, Type I baseline and work projections/directories. Legacy reads remain authoritative until the migration coordinator records completion; nonempty unmigrated writes fail closed.
- Fresh work projection identities/checkpoints and authoritative parity checks are present. V2 event normalization is present but unvalidated. Program-qualified keys and slimmer closure cells are not yet implemented.

## Validation boundary

These are earlier focused working-tree results, **not proof for the final checkpoint**:

| Evidence | Observed result | Limit |
| --- | --- | --- |
| AuditObservation domain regressions | 16 passed | Routes/history proof absent |
| Finding native receipt/malformed replay set | 14 passed | Later source/baseline/handler edits remain unconfirmed |
| Work projection/directory/authority set | 96 passed | Before final native-v2 normalization, program key and row-size work |
| Selected read-source/Finding/baseline set | 15 passed | Earlier state, before the later v2/baseline regressions |
| New actor/whitespace/truncated-history/owner-retry set | 5 failures and 2 passes | Fixes were written; confirming run did not finish |
| Cross-program work identity regression | Failed as expected | Fix is queued |

The final focused validation attempt was canceled after prolonged compiler activity under severe host memory pressure. It produced no completed test result. The parent dotnet process and its private compiler have exited; no result from that attempt counts as RED or GREEN. No full backend suite, final Release build, exact-head adversarial review, hosted CI, PR or merge has completed for this checkpoint.

Latest attempted command (full analyzers retained):

```sh
DOTNET_PROCESSOR_COUNT=2 DOTNET_gcServer=0 DOTNET_GCConserveMemory=9 \
NUGET_PACKAGES=/tmp/compliance-domain-nuget-20261022 \
dotnet test test/Compliance.Tests/Compliance.Tests.csproj -c Release --no-restore \
  -p:UseSharedCompilation=false -m:1 \
  --filter 'FullyQualifiedName~FindingCommandReceiptTests|FullyQualifiedName~FindingReadSourceTests|FullyQualifiedName~FindingBaselineMigrationTests|FullyQualifiedName~Features.Remediation.FindingTests'
```

Locked restore succeeded using the isolated package cache above. Credentials were loaded only into the restore command environment; no key or token is part of this checkpoint.

## First work on resume: finish #1026

1. Read the archived `compliance-finding-core-checkpoint-20261022.md` for exact prior commands and the full remaining checklist. Run the queued focused regressions to establish actual outcomes. New cases cover null retry collections, duplicate action identity, mutation of a closed native Finding and incomplete historical prefixes. Do not treat the canceled compiler run as a failure of those assertions.
2. Finish null-collection clean-failure guards and the pending replay/baseline corrections. In particular, as-of facts must form a contiguous prefix: revision 2 cannot be included when its initial raise is excluded. Apply both business and original recorded time; distinguish audit physical offsets from business versions. Preserve original migration lineage and exact legacy baseline digests.
3. Make Work keys program-qualified, verify the existing v2 normalization and canonical metadata identity guards, and store only action ID/owner/status/completer cells in closure rows. Prove accepted events and primary/index/checkpoint rows with actual serializer/SDK frames, including the oversized negative control.
4. Fix internal migration authorization through full production composition. Add the explicit single-step same-image runner/CLI with durable status, exact offset resume and no unrelated host effects. The current migration request still inherits the human program-management requirement, and there is no supported production caller.
5. Refresh `aggregate-command-coverage.json` and the boundary ledger. The inventory is **stale** relative to the seven new v2 facts and writer/helper routes; it must not be used as current acceptance proof.
6. Run focused and full backend units, format and Release build. Isolate the Finding delivery from the AuditObservation/report/decision work before opening its focused PR. Review the immutable head, resolve findings, require hosted CI, squash merge and read back acceptance. Keep all preserved work recoverable while splitting it.

Sole-coder execution remains serialized in this checkout. Prepared plans are archived under the recovery snapshot's `plans/` directory:

- `compliance-finite-domain-queue-20261022.md` and `.json`: all 102 baseline root responsibilities, plus AuditObservation; 69 scheduled review/change responsibilities and 33 preserve/prove identities. This is not a claim that 69 roots necessarily need code edits.
- `compliance-fitz-complete-frame-proof-20261022.md` and capture helper: actual public SDK/transport proof plan; not yet compiled or run.
- `compliance-program-bounded-state-slice-20261022.md`.
- `compliance-evidence-support-withdrawal-slice-20261022.md`.
- `compliance-audit-observation-routing-slice-20261022.md`.

## Broader implementation still open

Six selected facts are still undeclared: `EvidenceSupportWithdrawn`, `ControlOperatingPlanProposalWithdrawn`, `CampaignMaterialDelivered`, `CampaignDeliveryFailed`, `PolicyAcknowledgementDeclined`, and `TrainingCompletionSourceFailed`. Canonical `TenantOffboardingCompleted` still has no owner/emitter. AuditObservation's four operations remain unrouted. Other accepted existing producer and aggregate-boundary cuts remain in the finite queue.

Four own-attest placeholders are deliberately superseded by canonical ClientExport/hold facts; do not add duplicate ceremonies or emit them to inflate coverage. No production migration was executed, no legacy source was deleted, and no database was modified.
