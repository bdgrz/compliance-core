# Proposed-domain delivery reset — 2026-11-04

Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

**Paused at the user's request.** The current Program full run was canceled and
does not establish full acceptance. The [restart handoff](restart-handoff.md)
preserves the checkpoint and proposed concurrent-agent plan. Do not resume
implementation without the user's instruction.

PR #1186 has merged as `baffd1ba` after exact-head Backend CI passed 8,700 units.
Person writer retirement subsequently merged through PR #1187 as `4e4c9d01`;
exact-head and post-merge Backend CI both passed 8,707 units. The Program
target-date invariant repair is now the sole integrating slice. Evidence Support
withdrawal is prepared separately with explicit consumer/wiring gaps.
The reviewed context documents remain the specification. This is a delivery reset,
not a change to the domain scope or the user's Portia.Cassie direction.

## Verified delivery

Four domain batches merged through PRs #1179, #1181, #1182 and #1186. The
latest merge is `baffd1ba`. PR #1186's exact final head passed Backend CI with
**8,700/8,700** unit cases, after source/adversarial/Jev review. Its merged tree
matches the reviewed tree; independent post-merge Backend CI also passed.
No deployment or whole-domain completion is claimed.

PR #1186 delivers the next named-command business batch and the first nine
Portia.Cassie repositories, twelve application tables, schema/configuration and
native CI/test setup. Acceptance retained ordinary xUnit collection parallelism;
only shared native schema metadata setup is gated. The gate repaired five real
Portia projection-composition fixtures, incomplete-database cleanup and a fixture
whose future activation changed a work item's identity during queries. Production
Work selection was unchanged.

Review restored the Person replacement identity check, then fixed current-member
checks on service-owner review and corrective-change receipt replay. Focused
regressions observed the intended failures before repairs; 58 authorization cases
and the expanded 8,700-unit suite passed afterward. The startup readiness probe's
connection-reset retry was also exercised successfully in final-head CI.

The [requirement coverage map](requirement-coverage.md) records eight contexts
against actual owners/tests and named missing behavior. Other context maps remain
source-review handoffs; a map or passing old tests do not prove target acceptance.

The Person cutover observed seven retired-command failures and the old HTTP-route
failure before removing the writer. The first full gate passed 8,706 and failed
one unisolated RBAC presentation fixture; the fixture correction passed 13 focused
cases with SQL unavailable. Fresh assembled acceptance then passed **8,707/8,707**
in 12m47s with unchanged inputs. Its final-head CI/merge remain separate from the
already merged PR #1186 evidence.

## Preserved, unvalidated work

| Work | Preserved branch/head | Remaining gate |
| --- | --- | --- |
| Program date ordering | `feat/proposed-domain-impl`, current bounded integration | 6 intended RED failures, then 65 focused/native/history/HTTP cases passed; full gate and delivery pending |
| Seven Access Cassie repositories | `work/proposed-access-cassie` at `226d00d6` | Shared wiring, native tests and assembled validation |
| Five Program/scope Cassie repositories | `work/proposed-program-scope-cassie` at `f283c48b` | Shared wiring, native tests and assembled validation |
| Finding closure proposal/review/withdrawal | `work/proposed-assurance` at `527fda47` | Reopen authority, Work/Examinations consumers, public cutover, shared wiring and executed tests |
| Methodology name admission | `work/proposed-methodology`, separately listed milestone commits | Integration/wiring and executed validation; do not sweep branch ancestry |
| Member/person linkage | `work/proposed-workforce`, held `f17ae8a7` draft | Actual owner/handler implementation and validation; not GREEN |
| Evidence Support withdrawal | `work/evidence-support-withdrawal` at `3c81d366` | Authored/unexecuted owner/handler boundary; shared wiring, fixed-cut/current-use consumers and public read-side qualification remain |

Source-only commits and Jev probabilities are review inputs, not delivery evidence.
Do not integrate broad worker commit ranges: some branches intentionally contain
held owner changes alongside independently accepted migration fixes.

## What slowed delivery

The Cassie switch exposed published-engine SQL restrictions and test setup cost.
At the same time, parallel workers produced more changes than the coordinator could
wire, review, execute and merge. Shared integration and validation became the
bottleneck. Several existing business behaviors also differ from the reviewed
proposal; storage migration does not resolve those requirements.

Known operational/audit qualifications are explicit: native database teardown
[#1183](https://github.com/bdgrz/compliance-core/issues/1183), concurrent schema
startup [#1184](https://github.com/bdgrz/compliance-core/issues/1184), and native
fact-factory source-inventory limitations
[#1185](https://github.com/bdgrz/compliance-core/issues/1185).

## Working limits after this checkpoint

1. Finish the current integration gate before accepting another implementation batch.
2. Thereafter keep one business slice integrating and at most one preparing source.
   Other parallel capacity reviews the exact integration source or maps requirements.
3. Use one acceptance row per reviewed requirement: behavior, owning invariant,
   source/facts, public/internal wiring, executed evidence, and named remainder.
4. Continue source-first tests/Jev, then coordinator compilation and native/unit
   verification. A native SQL contract probe should precede broad adapter rollout.
5. Commit/push bounded source checkpoints with accurate validation status; delivery
   still requires final-head CI, merge and post-merge readback.
6. Keep framework gaps explicit. Portia owns projection execution; avoid building
   another paging, persistence or retry framework in this repository.
