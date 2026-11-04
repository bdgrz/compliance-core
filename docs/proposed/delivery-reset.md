# Proposed-domain delivery reset — 2026-11-04

Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

The current integration gate is finishing before another batch is accepted. One
Person writer cutover is prepared in isolation and held until this gate merges.
The reviewed context documents remain the specification. This is a delivery reset,
not a change to the domain scope or the user's Portia.Cassie direction.

## Verified delivery

Three domain batches merged through PRs #1179, #1181 and #1182. Main is
`97cecc07`; the last merged backend suite passed 8,431 cases. These results apply
to those revisions and their Fitz-backed read stores.

The current integration tree contains the next domain batch and nine Cassie
repositories, twelve application tables, configuration/schema startup and native
CI/test setup. Strict Release build and whole-solution whitespace verification pass.
The assembled backend unit suite passed **8,673/8,673** in 14m22s against a fresh
pinned native service with ordinary collection parallelism. Compiler inputs stayed
unchanged throughout that run. Final-head hosted CI and merge remain pending.
This result does not establish complete domain/context coverage.

The resumed gate repaired five production-composition fixtures, restoring real
registered Portia projector execution and separate native replay databases. A
72-case diagnostic run passed 70 and failed incomplete-database cleanup plus a
mixed-source timing fixture. The first repair now discards incomplete databases
without further DDL. The second backdates staged control/policy decisions and
freezes query time after all seeding: a future responsibility activation previously
changed a fallback work item identity mid-test. Production Work selection was not
changed. Independent review also restored the former Person replacement identity
check; its native regression was observed RED at the intended assertion, then
GREEN in the complete run, preserving stored state and checkpoint on refusal.

The [requirement coverage map](requirement-coverage.md) now records eight contexts
against actual owners/tests and named missing behavior. The other contexts still
need that requirement-level mapping; source review itself is not execution proof.

The exact-head review then found current-authorization gaps in service-owner review
and accepted corrective-change retries. A clean27-case selection observed13
behavioral failures; repairs passed58 focused cases and the expanded full unit
gate **8,700/8,700** in14m15s. The first hosted run stopped before units on startup
readiness connection reset; the GET probe now retries that error. Final-head hosted
CI and delivery evidence live in [PR1186](https://github.com/bdgrz/compliance-core/pull/1186).

## Preserved, unvalidated work

| Work | Preserved branch/head | Remaining gate |
| --- | --- | --- |
| First nine Cassie repositories and batch-4 business behavior | `feat/proposed-domain-impl`, integration tree | Full backend suite, final review, exact-head CI and merge |
| Seven Access Cassie repositories | `work/proposed-access-cassie` at `226d00d6` | Shared wiring, native tests and assembled validation |
| Five Program/scope Cassie repositories | `work/proposed-program-scope-cassie` at `f283c48b` | Shared wiring, native tests and assembled validation |
| Finding closure proposal/review/withdrawal | `work/proposed-assurance` at `527fda47` | Reopen authority, Work/Examinations consumers, public cutover, shared wiring and executed tests |
| Methodology name admission | `work/proposed-methodology`, separately listed milestone commits | Integration/wiring and executed validation; do not sweep branch ancestry |
| Member/person linkage | `work/proposed-workforce`, held `f17ae8a7` draft | Actual owner/handler implementation and validation; not GREEN |
| Program date ordering | `work/proposed-program-dates` at `adcc1348` | Planning only; no code/tests authored |

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
