# Resumed 2026-11-05 — assembled build repaired, native acceptance blocked

The user explicitly resumed with “pickup where we left off.” The integration
checkout was clean at `e3c31259`; all 21 preserved WIP branches remain intact.
Live `origin/main` remains `4e4c9d01`, and no open PR targets the integration head.
The former usage-stop text below is historical; the usage/credit balance remains
invisible to this session. No parallel agents were started in this resumption.

Restored the isolated `ListEvidenceSupports` namespace correction from
`f7e4d495` as `59d5e0af`. Fresh strict compilation exposed further unfinished
integration: a shadowed Application local, the wrong Superseded outcome member,
missing extension/test imports, incompatible fixture APIs, and formatting/analyzer
errors. These were repaired without changing analyzer enforcement. Access
composition tests assert persisted checkpoint progress through the public runner;
they no longer attempt to read Portia's internal `Projector.Store` property.

Actual focused RED: 141 passed / 7 failed. Six replay cases lacked generated JSON
metadata for Finding proposal facts; one Support replay fixture attached event
metadata twice. Added the four new Finding fact registrations and reused the
fixture's already-attached events. Focused GREEN: **148 passed / 0 failed** across
Support withdrawal, Finding closure proposal, Controls lifecycle/terms, occurrence
lifecycle, Policy lifecycle and inventory terminal lifecycle tests.

Strict Release build passed with **0 warnings / 0 errors**. Whole-solution whitespace
verification passed. Evidence logs are local at
`/tmp/compliance-resume-{build,focused,format,native}-20261105.log`.

The single native schema probe still fails before behavior assertions:
`CassieProjectionDatabase.InitializeAsync` cannot rebuild cardinality statistics
for `portia_projection_checkpoints`: “transactional DDL cannot access multiple
databases.” The pinned image remains `14c99b2e…`. Recorded reproducible blocker
[#1188](https://github.com/bdgrz/compliance-core/issues/1188), distinct from concurrent
startup #1184. No engine workaround, dependency rollback, skipped native test,
full-suite pass, PR, CI or merge is claimed.

Next: resolve #1188 through a published compatible dependency, rerun the schema
probe and focused native consumers, then full backend units. Independent WIP
source integration remains possible using the preserved branch manifest; never
sweep broad ancestry. Full reviewed-domain source completion remains partial.

---

# Worktree cleanup — latest recovery locations

All uncommitted source was committed before cleanup. The 23 auxiliary worktrees
were removed at the user's request; their historical paths below no longer exist.
Unique committed work is preserved on **21 local and remote `wip/*` branches**,
verified at exact commit tips. The primary checkout remains on
`feat/proposed-domain-impl`. Obsolete local branches and remote aliases were pruned.
Implementation stays stopped under the user's usage/credit constraint.

Use the [cleanup manifest](restart-artifacts/2026-11-04/worktree-cleanup-manifest.json)
for old-branch → WIP-branch mappings, exact tips and the verified full recovery
bundle. Recreate only the worktrees needed after an explicit resume, for example:

```sh
git worktree add ../compliance-core-worktrees/readiness-code wip/resume-readiness-code
```

These are preservation checkpoints, including incomplete and unvalidated code.
They are not a whole-domain build, test or delivery claim. Do not sweep broad WIP
ancestry when integrating. Earlier state inventories below are historical.

---

# Usage-boundary stop — latest checkpoint

The user instructed: “stop when we hit our usage limit, do not consume credits.”
The included-usage balance and automatic credit boundary are not visible to this
session. All parallel agents were therefore stopped conservatively. Do not resume
automatically. Source completion remains partial; preserve every worktree,
including uncommitted files. The user's latest implementation priority remains
code complete across the reviewed domain, with qualification tracked separately.

Current recovery inventory: [usage-stop manifest](restart-artifacts/2026-11-04/usage-stop-manifest.json).
The integration branch contains prepared Access, Support, Program/scope, Controls,
Policies, Risk/provider and Assurance changes, with shared wiring partly assembled.
Other context workers have additional isolated commits and uncommitted source in
the inventoried worktrees. Never sweep their broad ancestry. No whole-domain
compilation, test, PR or merge acceptance is claimed.

Program date repair independently passed strict build/whitespace and 8,742 units
at `5feb9922`, on the prior Cassie digest. It remains local, not delivered.
The newly pinned Cassie digest is `14c99b2e…`; fresh native initialization then
failed with “transactional DDL cannot access multiple databases” while rebuilding
cardinality statistics. That failure is not observed behavioral RED. The combined
validation worktree holds a one-line Page namespace compile correction; inspect
its next build log before deciding any further fix. No engine workaround applied.

The older checkpoint and resumed plan below are historical.

---

# Paused implementation and agent plan

**Resumed 2026-11-04 by explicit user instruction:** “Read
docs/proposed/restart-handoff.md and resume processing with many parallel agents.”
The paused state below is the historical recovery checkpoint. The active queue is
Program date qualification/delivery, with Support withdrawal and Access Cassie
preparing in exclusive feature paths. Additional agents review consumers, shared
wiring and held packets. One isolated worktree owns local .NET execution; the
coordinator remains the only shared-file writer. Preparation is not acceptance.

Saved 2026-11-04 for a Codex restart. **Implementation is paused at the user's
explicit request. Saving this checkpoint does not authorize resuming it.**

## Start here after restart

1. Read this document, `AGENTS.md`, [delivery state](delivery-reset.md), and
   [requirement coverage](requirement-coverage.md).
2. Inspect `git status`, branch/head, worktrees and live GitHub state before
   changing anything. Preserve all work. Local `main` was stale at `97cecc07`;
   last verified `origin/main` was `4e4c9d01`. Do not reset or clean worktrees.
3. Stay paused until the user explicitly resumes implementation. Existing agent
   sessions/tool IDs should not be assumed to survive restart.
4. When resumed, use the saved queue and operating plan below. Do not interpret
   prepared branches, names, audits or focused GREEN as completed workflows.

Suggested restart prompt:

> Read docs/proposed/restart-handoff.md, verify the saved state, and report back.
> Keep implementation paused until I explicitly resume it.

## Objective and continuing constraints

- Implement the full reviewed `docs/proposed/contexts/*` backend domain. The locked
  proposal is the specification. Full layers were explicitly authorized: owners,
  events, handlers, reactors, read models and API/MCP wiring. Aggregate-only defaults
  do not narrow that earlier full-scope authorization.
- Work in this backend repository. Client and upstream framework implementation
  are outside scope. Backward compatibility and rollback were explicitly waived.
- Write business behavior: aggregate invariants, handlers invoking owners, reactors
  mapping committed facts to commands. Portia owns framework execution, persistence,
  concurrency and delivery/retry mechanics. Avoid another application framework.
- Use Portia.Cassie for read-model repositories; Fitz remains the event store.
  Authoritative aggregate/history readers remain authoritative. Do not blindly
  migrate every legacy store; some owners are superseded by the approved model.
- Use exclusive file ownership/worktrees. The coordinator alone edits shared
  registrations, serializers, composition roots, schema composition, catalogs and
  aggregate audit ledgers. Workers supply exact shared-change instructions.
- Preserve real behavioral TDD and bounded Jev review. Authored/uncompiled tests
  are preparation, not observed RED. Jev signals never replace source or execution.
- Commit/push bounded checkpoints with honest validation status. Delivery still
  needs final-head review/CI, squash merge and post-merge readback. Do not close
  umbrella [#1176](https://github.com/bdgrz/compliance-core/issues/1176) while partial.

## Last verified delivery

Person generic-writer retirement merged through
[PR #1187](https://github.com/bdgrz/compliance-core/pull/1187) as
`4e4c9d0129e0390df270695565ea4b21291df66c`. The merged tree matched reviewed
`a3e501f3`; local, final-head hosted and independent post-merge acceptance each
passed **8,707/8,707** units. Integration branch readback/push completed.

The preceding PR #1186 delivered named business behavior and the first **nine**
Cassie repositories / twelve application tables. This is not whole-domain or
deployment qualification. Person member-link cardinality/reverse uniqueness and
accepted HRIS application remain open.

## Paused checkpoint and queue

The captured integration base was `1deb9cf7121f9d49ab1ee0e60bda4ea52f223455`
on `feat/proposed-domain-impl`. This save checkpoint preserves the already-authored
Program work and documentation; it does not mark that work accepted or merge it.

| Packet | Preserved source | Actual status / remaining acceptance |
| --- | --- | --- |
| Program date ordering | Existing worktree changes saved in this checkpoint; original preparation `f818f45f` then `784bfca2` | Six-line rule repair, 35 new cases. Actual RED: 29 passed / 6 failed. GREEN: 65 focused/native/history/HTTP passed. Build/whitespace passed. Full run was canceled on pause: **no completed full gate**. No PR/merge for this repair. |
| Evidence Support withdrawal | `work/evidence-support-withdrawal`: tests/contracts `6307b091`, implementation/review `3c81d366` | Authored/unexecuted and held. JSON/DI/Lead grants/transport plus cut-safe Examinations history, active Advisor admission, Applications current status/freshness and public Get/list status need coordinated completion before enablement. |
| Access Cassie | `work/proposed-access-cassie` at `226d00d6`; isolated commits `f6cdc122`, `d0d29064`, `6d880189`, `226d00d6` | Seven repositories/eight tables, unvalidated. Shared schema/DI and feature fixtures need integration. Preserves legacy permission behavior; does not complete scoped/custom roles or AccessGrant retirement. Local branch had no upstream, though cached origin ref matched. |
| Program/scope Cassie | `work/proposed-program-scope-cassie` at `f283c48b`; isolated commits `132a49ad`, `2a9be0d4`, `f283c48b` | Five repositories/nine tables, unvalidated; shared wiring/fixtures and explicit row/body identity qualification remain. |
| Finding closure | `work/proposed-assurance` at `527fda47` | Held source, not accepted. Reopen authority, Work/Examinations consumers, public cutover, wiring and execution remain. Do not import owner-only changes that break old closure without the coordinated cutover. |
| Methodology name admission | `work/proposed-methodology`; worktree tip at capture `65d57ff9` | Held and unvalidated. Inspect exact candidate commits/reviews, not whole ancestry. Earlier isolated chain: `ee93911f`, `bc880ad8`, `f30019be`, `ff5f644d`, `b636ed03`, `92d03d20`. |
| Member/person linkage | Held `f17ae8a7` draft on workforce ancestry | Tests/stubs are not implementation. Authoritative uniqueness, multiple links and full wiring remain. |

Do not sweep broad worker commit ranges. Some branches contain unrelated held
work or already-delivered ancestors. The artifact manifest includes the captured
worktree inventory; reverify refs before integration.

Program historical-data qualification: strengthened validation rejects previously
stored reversed-across-null plans on historical reads/reconstruction; fresh replay
can stop at an old invalid fact even after a later valid correction. This is
accepted under no-backcompat scope. Fresh data or explicit conversion may be
needed; no conversion/deployment is claimed. See the
[date review](reviews/program-target-date-ordering.md).

## Proposed concurrent agent structure

Use concurrency to complete integration packets and separate validation from
integration. The following is a saved recommendation, not a resumed execution.

| Persistent role | Ownership |
| --- | --- |
| Coordinator | Dependency/scope decisions, shared files, assembled integration, final CI/merge and truthful coverage. |
| Business-slice worker | Complete one capability's owners, handlers, feature-local consumers/read behavior/tests; supply exact shared wiring. Initial candidate: Support withdrawal. |
| Second slice worker | Complete another independent packet including its feature-local fixtures. Initial candidate: Access Cassie, with the prepared fixture checklist. |
| Validation/review worker | One serialized local .NET lane in an isolated, immutable validation worktree: actual RED/GREEN, strict build, full units, formatting, failure classification and independent head review. |

- One candidate validating, at most two ready packets. When the queue fills,
  agents help close its gaps instead of starting another slice.
- Shared-file ownership stays with the coordinator. Handoffs must include explicit
  owned commits, interfaces/contracts, consumer effects, fixture needs, test filters,
  shared changes, retirements and remaining acceptance.
- Freeze the validating checkout at an exact SHA. Workers continue only in their
  own worktrees; never edit the tree whose tests are running.
- Assemble coherent batches before full validation, preserving focused behavioral
  RED. Run the required full suite and final-head CI. Repeat after justified source
  changes/failures, not on every agent's partial handoff.
- Overlap hosted CI for an immutable reviewed head with preparation of the next
  local candidate. A changed head requires qualification of that head.
- Start by qualifying the saved Program-date packet; finish Support's consumer
  gaps and Access's fixture preparation concurrently when the user resumes.

The machine Codex config was changed, at the user's request, from 12 to **20** at
`features.multi_agent_v2.max_concurrent_threads_per_session` in
`~/.codex/config.toml`. Other settings and mode 0600 were preserved; TOML parsing
and `codex features list` succeeded with installed codex-cli 0.145.0. No global
config contents or credentials are included here. This conversation's enforced
runtime limit was four active agents total; verify the new session's actual limit.
Capacity does not require twenty simultaneous implementation packets.

## Durable evidence and restart checks

[Artifact manifest](restart-artifacts/2026-11-04/manifest.json) lists hashes and
captured source paths. This folder preserves 16 context source/assertion handoffs
alongside the eight already recorded in requirement coverage, plus Support/Access
integration reviews, source hashes and Program RED/GREEN/canceled-run excerpts.
Use the [portable coverage index](restart-artifacts/2026-11-04/coverage-index.json).
Historical `/tmp` paths inside copied artifacts are original evidence pointers;
the copied basenames here are the durable versions. These maps are not fresh
runtime acceptance or proof that a context is complete.

The recovery patch is only a backup of the original paused Program code diff.
That code is also preserved by this checkpoint. **Do not blindly reapply it.**

After explicit resume: inspect the working tree, reverify live branch/CI state,
confirm source hashes, and allocate ownership before running commands. Use the SDK
from `global.json`; serialize local .NET commands (`-m:1`), retain ordinary xUnit
collection parallelism and the existing native schema setup gate. Use the pinned
native Cassie test service and isolated database leases. Do not weaken analyzers,
replace real authorization/projection tests with permissive fakes, or manufacture
success from an interrupted run.

Jev wrapper `scripts/backlog-jev.mjs` was absent in this checkout. Prior bounded
reviews used the TypeSafe System One API with `jev-latest` resolving to
`jev-1.14.0`. Recheck available tooling when needed; keep the key only in the
command environment, never in logs, artifacts or Git. The configured key file path
is `~/.config/typesafe/jev.key`, not a checked-in credential.

Open qualifications include #1178 (broker integration), #1180 (Finding source
vocabulary), #1183 (native teardown), #1184 (concurrent schema startup), and #1185
(native fact-factory inventory). Full-domain, client, deployment and release
qualification remain separate. No implementation should restart merely because
this document was loaded.
