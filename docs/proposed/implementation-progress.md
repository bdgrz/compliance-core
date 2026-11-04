# Proposed domain implementation progress

The locked context documents are the specification. This ledger records implemented
behavior and validation; a matching type name or a passing focused test does not
establish completion of an owner, workflow, or context.

Tracking issue: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

## Delivery baseline

- Integration branch: `feat/proposed-domain-impl`.
- Starting commit: `c6d16cdb` (Portia dependency prerequisite, issue #1175).
- Dependency validation: Release build and format passed; all 8,053 backend units
  passed on `c6d16cdb`. Twelve focused migration cases and the five corrected processor
  assertions also passed. Dependency PR #1177 merged as `6051c4cb`; final-head and post-merge Backend CI passed.
  Isolated broker validation passed startup and failed two assertion probes; those
  failures are parked explicitly in #1178, without an end-to-end acceptance claim.
- Business scope includes documented aggregates, events, commands, handlers, reactors,
  read models, authorization and API behavior. Client implementation stays in client repositories.
- Suspected design problems are recorded for later evaluation; they do not authorize
  changing the reviewed proposal. Portia owns execution, persistence, concurrency,
  delivery and retries.

See the [delivery reset](delivery-reset.md) for the current implementation freeze, preserved worker checkpoints and reduced work-in-progress limits.

## Active batches and exclusive ownership

The coordinator owns registrations, serialization contexts, composition roots,
shared catalogs and fixtures, and all audit/coverage updates. Workers report exact
wiring requirements and edit only their assigned business slice.

| Worker worktree | Exclusive business paths | First bounded batch | State |
| --- | --- | --- | --- |
| `workforce` | `Features/Workforce/**` in Common, Core, App and feature tests | Start, leave, return and end WorkRelationship lifecycle | Batch 2 merged; next source batch prepared |
| `assurance` | `Features/Remediation/**` in Common, Core, App and feature tests | Product Waiver request, independent review and revocation | Batch 2 merged; next source batch prepared |
| `methodology` | `Features/MethodologyTemplates/**` in Common, Core, App and feature tests | TemplateVersionApplication per-version entry outcomes | Batch 2 merged; next source batch prepared |

Workers submit test-first and implementation commits. Following the user's updated
instruction, they now use source review and live Jev before any compilation or test
execution. The coordinator accumulates completed source slices, wires one assembled
batch, then runs the required checks. Authored tests are not recorded as observed
RED, and prepared source is not recorded as validated GREEN. Blocked work is parked with a named missing condition
while independent work continues.

## Cassie read-model direction

The user selected **Portia.Cassie for read models** after the existing Fitz KV
implementation was identified. The [migration inventory](read-model-migration.md)
records 82 existing KV projection stores, with nine assigned to the first conversion
batch and 73 still remaining. The coordinator owns the native package, configuration,
application schema startup, Compose/CI service and real database test seam. Feature
workers own business SQL repositories and queries. Fitz retains event persistence.
The next merge is held until the assembled Cassie path is validated. Existing
source batches and prior green Fitz-backed tests do not qualify the new database path.

The latest pre-conversion batch-4 checkpoint built with zero warnings/errors and
ran 1,928 affected cases: 1,920 passed and eight failed. Two failures exposed a
historical CorrectiveAction summary being reconstructed from changed terms;
the source repair preserves the original authored description. Six Workforce
failures involve retired generic mutation fixtures and refusal-message compatibility;
source fixes passed the subsequent 56 business cases (the same run failed two new native-fixture cleanup cases). The native seam then passed four schema/data/database-isolation checks after table cleanup was corrected and the pinned engine database-drop problem was parked in #1183. The strict Release build passed with zero warnings and errors. No full-suite or feature-repository Cassie acceptance
is claimed for batch 4 yet. Jev's contradictory broad-versus-specific service
permission judgments remain advisory; the actual authorization cases passed in
this checkpoint and no authorization rule was weakened on that model signal.

The resumed assembled gate is now locally GREEN: strict Release build has zero
warnings/errors; whole-solution whitespace verification and all six Node tooling
cases pass; proposal and runtime-fixture validators pass. The full backend suite
passed **8,673/8,673** with BrokerIntegration/WebIntegration excluded, using a fresh
pinned native Cassie service and ordinary xUnit collection parallelism. Changed
compiler inputs were hashed before the run and verified unchanged afterward.
Local log: `/tmp/compliance-cassie-integration-full-unit.log`; TRX:
`/tmp/compliance-cassie-integration-green-results/backend.trx`. Hosted final-head CI,
merge and post-merge readback remain pending.

Five old production-composition fixtures now lease native databases and run real
registered projectors. Incomplete leases are discarded without further DDL.
Mixed-source list/detail verification freezes query time after historical setup,
preventing responsibility activation from legitimately changing a work identity
mid-assertion. The bounded Person projection identity regression was observed RED
before restoring the previous replacement guard and is GREEN in the full run.
These repairs do not implement any of the separately mapped remaining domain rules.

Exact-head adversarial review of PR [#1186](https://github.com/bdgrz/compliance-core/pull/1186)
then found two current-authorization gaps outside that test selection: a linked
person-owner could review through stale projected membership, and native accepted
corrective-action retries skipped the prepare-only permission check. New source
membership and retry cases produced a clean **14 passed / 13 failed** RED selection.
After the repairs, **58/58** relevant cases and the expanded full backend suite
**8,700/8,700** passed (14m15s), with unchanged compiler inputs. The strict build
and whole-solution whitespace verification pass. Service person/team owner paths
now use authoritative current membership and UTC expiry; corrective changes check
acting membership and current authority before receipt replay while retaining
new-write checks. Privileged frozen retries do not require the current Finding body.

Hosted CI at `cbaaba5f` passed spec/restore/format/build but stopped before unit
execution on native readiness connection reset (curl56). Its idempotent readiness
GET now retries all startup errors. A local restart also reproduced and recovered
from an empty reply. Final-head hosted CI and merge evidence are tracked in PR1186;
the earlier hosted failure is not counted as unit acceptance. The separate Person
generic-writer source preparation remains held and unvalidated.

Source inventory caveat: the batch-4 export contains 568 facts, 136 aggregate types,
819 requests and 777 Core handler registrations. It still reports 66 unwired producer
operations. These are syntactic navigation counts. In particular, it misses the
native Person.Append → PersonContent.Fact factory path and underreports four native
person publishers/command paths; fixture notes and #1185 record that limitation.
The exporter result is not a runtime completion claim, and reverse membership-link
work remains partial.

## Context coverage

“Not mapped” means the complete reviewed requirements have not yet been reconciled
against executable behavior and tests. It does not mean that the context has no code.

| Reviewed context | Implementation evidence | Remaining work / wiring |
| --- | --- | --- |
| [Platform access](contexts/platform-access.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Firm services](contexts/firm-services.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Program and scope](contexts/program-and-scope.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Workforce assurance](contexts/workforce-assurance.md), [requirement map](workforce-implementation.md) | Partial: named WorkRelationship lifecycle/changes and computed unsponsored gap; retired service identities are terminal | Full requirement mapping and workflow validation pending |
| [Application inventory](contexts/application-inventory.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Technology and information inventory](contexts/technology-and-information-inventory.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Examinations](contexts/examinations.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Auditor collaboration](contexts/auditor-collaboration.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Commitments](contexts/commitments.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Criteria](contexts/criteria.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Control environment](contexts/control-environment.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Evidence](contexts/evidence.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Retention](contexts/retention.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Assurance work](contexts/assurance-work.md) | Product Waiver workflow plus Finding severity and CorrectiveAction cancellation locally covered | Finding/CorrectiveAction proposed migration and waiver consumers remain |
| [Policies](contexts/policies.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Risk and provider oversight](contexts/risk-and-provider-oversight.md) | Partial: treatment reviewer independence; see existing owner review | Full requirement mapping and workflow validation pending |
| [External access governance](contexts/external-access-governance.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Change and incident oversight](contexts/change-and-incident-oversight.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [System description](contexts/system-description.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Readiness](contexts/readiness.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Advisor collaboration](contexts/advisor-collaboration.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Exports](contexts/exports.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Firm methodology](contexts/firm-methodology.md) | Per-version outcomes; separate draft owner and committed-source draft opener locally covered | Draft/line migration, entry materialization and parent completion remain |
| [Work queue](contexts/work-queue.md) | Not mapped | Full requirement mapping and workflow validation pending |

The existing [owner reviews](README.md#aggregate-implementation-evidence) remain
valid only for the behavior and validation they explicitly record. Commercial
operations are not designed in this proposal; context-specific documented deferrals
remain explicit rather than silently counted as delivered.

## Batch evidence

Batch 1 RED assembled WorkRelationship lifecycle, product Waiver and
TemplateVersionApplication requirements: 78 focused cases executed, 54 failed and
24 passed against the missing-behavior stubs. Failures include rejected valid
lifecycle transitions, missing waiver facts and ignored lifecycle projection facts.
Initial harness compilation exposed static-member analyzer diagnostics and a missing
test import; those are harness failures, not behavioral RED evidence. No analyzer
rules were changed. The role-seeding regression also failed with 36 grants instead
of the required 40, then passed after the four approved waiver grants were restored.

Batch 1 local assembled validation passed: 176 focused cases, 8,175 full backend
unit cases, six Node contract cases, proposal/coverage checks, whitespace verification,
and Release build with zero warnings/errors. The exported inventory was inspected:
11 new facts, two new owners and ten registered request handlers. These counts are
navigation evidence; they do not establish complete proposed-domain coverage.
Batch 1 PR [#1179](https://github.com/bdgrz/compliance-core/pull/1179) passed final-head
CI and adversarial/Jev review, then merged as `8dc0f40e`. Post-merge CI also passed.

### Batch 1 implemented behavior and remaining work

| Slice | Implemented and locally tested | Wired centrally | Explicit remaining work |
| --- | --- | --- | --- |
| WorkRelationship (WF-01/WF-03) | Start, leave, return and end decisions; named facts; revision and actor/date/reason handling; primary and observation projections; leaver/unowned consumer status; all four named facts replayed | Four handlers, JSON, HTTP/MCP actions and native response contracts | Manager/department/sponsor/date-correction commands; initial-record and source-reconciliation conformance; complete workflow and competing-append coverage |
| Product Waiver | Exact immutable rule/record scope; request, independent approval/rejection, computed expiry and reasoned revocation; terminal retries; point read and permission/transport guards | Four handlers, authorizer/reader, JSON, role grants, HTTP; request/read MCP only, personal review/revoke HTTP only | Control-evaluation and data-flow waiver consumption; explicit competing-append proof; remaining Finding/CorrectiveAction specification |
| TemplateVersionApplication (FM-03/FM-05) | One immutable entry set per applied version; one created/proposed/refused outcome per entry; exact completion counts; replay; committed application source verification and refusal; failed opening checkpoint retention | Two internal handlers, System-only authorizer, opening reactor and JSON; no public routes | Entry materialization and refusal choreography; parent completion; application identity/order migration; draft/line migration and removal of superseded contribution behavior; explicit competing-append proof |

### Batch 2 source-first checkpoint

The batch contains these integrated source slices. Release compilation passed with
zero warnings/errors and all 797 affected business tests passed. All 8,327 full backend units and whitespace validation passed; the table distinguishes earlier RED evidence
from test-first cases first executed at the assembled checkpoint.

| Slice | Prepared behavior | Observed evidence / remaining work |
| --- | --- | --- |
| Finding severity (AW-07) | Admit/replay only low, moderate, high; keep Work priority vocabulary separate | Earlier 22-case RED: 14 failed, eight passed. Earlier focused GREEN: 65 passed. Included in the successful assembled affected suites. |
| WorkRelationship named changes | Manager, department, sponsor and reasoned date corrections; owner guards, handlers and both projections; manager-chain default Lead grant | Earlier 61-case RED: 54 failed, seven passed. Named implementation and role wiring passed the assembled affected suites. Initial record/source reconciliation remains. |
| WorkRelationship unsponsored (WF-06) | Compute current sponsor eligibility with existing observation catch-up; retain field restrictions; no domain fact | 13 cases authored before implementation, first executed successfully in assembled affected suites. Whole-context validation pending. |
| MethodologyTemplateDraft | Separate draft owner, all-author reviewer exclusion, third-person approval, current-review clearing, terminal retry rules; immutable content | Earlier 19-case RED: 18 failed, one passed. Owner implementation passed assembled affected suites. Human commands and line publish/discard choreography remain. |
| Draft opening | Internal System-only command verifies committed line-start source; global reactor maps source fact | Eight cases authored before implementation, first executed successfully in assembled affected suites. Actual line-start producer remains pending. |
| Corrective action cancellation | Reasoned Lead cancellation; terminal fences; independent finding closure treatment; work removal and baseline history retain separate cancellation attribution | Owner/workflow/consumer tests first executed successfully in assembled affected suites. AW-09 finding-owned outcome reporting remains pending. |

Worker source reviews and coordinator shared-wiring review called live `jev-1.14.0`.
Exact questions, source manifests, probabilities and response artifacts are retained
in the session's `/tmp/compliance-*-jev-*` files. These are advisory first checks,
not proof of successful compilation or runtime behavior. Coordinator wiring review
adverse probabilities ranged 0.05–0.18; direct source inspection found no blocker.

### Parked items and validation failures

- The first assembled full suite ran 8,327 cases: 8,307 passed and 20 failed.
  Nineteen failures came from two outside Finding seeds still using the removed
  `medium` severity; the remaining failure was five catalog rows using an issue
  URL where the existing schema requires a numeric issue ID. Both source seeds and
  the metadata were corrected. The 51-case affected rerun passed; the complete
  rerun passed all 8,327 cases with zero failures/skips. No production guard or
  assertion was weakened.

- Approved Finding source vocabulary requires coordinated authoritative producers;
  legacy AuditObservation binds `examination_item`, which cannot simply be renamed
  to the proposed `audit_test_exception`. Tracked explicitly in
  [#1180](https://github.com/bdgrz/compliance-core/issues/1180); proposal authority is unchanged.
- Four concurrent compiles previously saturated the 24 GB machine. Cancelled runs
  are neither passes nor behavioral RED. Coding and Jev reviews remain parallel;
  local execution is deferred to assembled checkpoints and serialized with unchanged
  analyzer/test enforcement.
- Isolated broker assertion failures remain parked in [#1178](https://github.com/bdgrz/compliance-core/issues/1178).

No context is marked complete. The next accepted batch must retain requirement
mapping, actual RED/GREEN evidence, deferred wiring, remaining work and validation
failures rather than replacing them with name or test-count assertions.

Batch 2 final local acceptance: Release build with zero warnings/errors; 797 affected
business cases; 51 fixture/catalog correction cases; all 8,327 backend units with
zero failures/skips; whitespace verification; six Node cases and proposal/coverage
checks. The inspected inventory adds 12 facts (one explicit line-start producer
gap), one owner and six registered requests. Counts describe source navigation,
not whole-domain completion. PR [#1181](https://github.com/bdgrz/compliance-core/pull/1181) passed exact-head
Backend CI and merged to main as `7df499fd`. The integration merge preserved the
next source batch byte-for-byte; post-merge main CI also passed.

## Next source batch (unvalidated)

Human methodology draft commands/handlers are integrated with central registration,
JSON and platform HTTP wiring. Revision/discard are exposed to MCP; personal review
and approval are HTTP-only. Fourteen tests were authored before implementation but
have not been executed. The legacy internal handler has no public route to cut over.

Initial WorkRelationship provenance is integrated through the existing command,
recorded fact, owner and primary read view. The stable worker identifier still
determines identity; external IDs/revisions remain case-sensitive provenance.
Nineteen tests were authored before implementation but have not been executed.
Initial pending/active and sponsor admission changes are a separate source slice.

Both slices received live Jev source review (`jev-1.14.0`). These source checkpoints
are not compiled/tested acceptance. AW-09 finding-owned corrective outcomes and
methodology line choreography are being prepared independently. No new local .NET
run will start until sufficient source and central wiring are assembled.

### Batch 3 assembled source checkpoint

Prepared source now includes 104 authored cases across human draft commands (14),
line start/publish/discard choreography (21), initial workforce provenance (19),
initial lifecycle/sponsor admission (22), and finding-owned action outcomes (28).
No pre-implementation execution or RED is claimed for this batch. All source slices
received live Jev review before assembly. Central handlers, authorizers, JSON,
reactors, HTTP/MCP and native response wiring are assembled.

The Operations fixture now delivers original committed action records through the
real outcome reactor; direct Complete/Cancel still leave a reporting-lag window.
Close convenience accounts only for revisions appended by its own delivery,
preserving prior staleness: expected-after minus actual-after equals supplied-before
minus actual-before. This direct source/algebra check resolved Jev's uncertain
0.34 staleness candidate; runtime qualification is pending. Other assembled adverse
questions ranged 0.04–0.22. Personal draft review/approval have no MCP registration.

New-line public creation/name admission, retirement transport, truthful V4 reads
and ordered application support remain explicit; legacy DTO attribution is not
fabricated. New line producers are present in this source batch. Generic workforce
revision retirement is a separate next slice. Full compiled/behavioral acceptance
will be recorded after the combined checkpoint.

Batch 3 compilation failures before behavioral execution: IDE0007 required an
inferred local type in publication comparison; then the shared App composition
missed the methodology command namespace import. Both were corrected without
changing analyzer enforcement. These are assembly failures, not behavioral RED.

Batch 3 local runtime acceptance passed: zero-warning/error Release compilation;
69 affected regression cases after the initial 2,186-case run found eight fixture
failures; all 8,431 backend units with zero failures/skips; Node/proposal/coverage
checks passed. Full whitespace verification also passed. The eight
initial failures were corrected by real external DispatchAsync contexts, explicit
expected business refusals, delivery of committed outcomes in seeds, and a valid
bound cancellation-proof setup; no production guard or assertion was weakened.

This is a breaking Finding history change: replayed closure admission/finalization
requires prior finding-owned action-resolution facts. Legacy closed histories that
lack them need a separate stream conversion or fresh data; backward compatibility
and rollback were explicitly not required. No deployment/data conversion is claimed.

Additional compilation-only corrections included test naming/assertion conventions,
whitespace, the reporting fixture's nullable result switch and a seed helper's
resolver qualification. Existing rules stayed enforced. The final compiled source
and all 8,431 units passed after those corrections; these failures were not counted
as observed behavioral RED. The inspected inventory has five additional facts,
eight additional registered requests (three internal-only), and explicit missing
new-line creation/retirement routes. Counts remain navigation evidence only.

Batch 3 PR [#1182](https://github.com/bdgrz/compliance-core/pull/1182) passed exact-head
Backend CI and adversarial/source/Jev review, then merged as `97cecc07`.

## Batch 4 assembled source checkpoint

Prepared source includes 175 authored cases: generic WorkRelationship retirement
(16), named ServiceIdentity behavior/authorization/legacy preservation (105),
corrective-action changes (22), truthful V4 line queries (14), and named Person
details (18). These were authored before coordinator compilation; they were not
observed RED at that source handoff. Subsequent build/test evidence and remaining
validation are recorded above and in the read-model migration ledger.

Central wiring adds explicit Lead/Contributor grants and typed routes/JSON/handlers.
ReviewServiceIdentity is typed HTTP callable and invocation-guarded, with no MCP
registration. Broad generic WorkRelationship/ServiceIdentity routes/tools are
removed; registered legacy handlers uniformly refuse and historical reducers remain.
The service-specific defaults add four grants and corrective change adds one, for
47 bootstrap grants; explicit role assertions cover each.

V4 public methodology query DTOs replace the old authored-version shape without
fabricated attribution. The directory uses MethodologyPublicationDirectoryV2 and
projection-v2 storage to replay both event families. Ordered applications and new
line creation/name admission remain pending, alongside Person member cardinality
and source reconciliation. At the batch-4 checkpoint, legacy RevisePerson still
wrote generic changes; its later retirement is tracked below.

Live Jev shared-wiring first review flagged an uncertain broad-grant question0.51.
Narrow followup returned contradictory judgments (broad-only allowed0.92, specific
manage key queried0.98). Direct source selects the specific permission, whose string
differs from the broad key; the authored authorization regression explicitly denies
the broad-only grant. This advisory candidate remains for the assembled runtime
check; no implementation change is justified without a failing source-backed test.
Other assembled adverse signals ranged0.03–0.23.


## PR #1186 delivery and Person writer continuation

Batch 4 and the first nine Cassie repositories merged through
[PR #1186](https://github.com/bdgrz/compliance-core/pull/1186) as `baffd1ba` after
final-head Backend CI passed **8,700/8,700** units. The merged tree matches the
reviewed head. Independent post-merge Backend CI also passed on `baffd1ba`. This result applies to
the delivered slices, not completion of the approved domain.

The sole current integration is Person generic-writer retirement. Before the
production change, seven focused checks failed at the intended retired-command
Conflict/no-I/O assertions; the old public revision route also failed its removal
check by returning 200. Historical V2 continuation reached its privacy,
correlation, attribution and frozen-receipt assertions in that RED run. The
assembled source removes the generic Operations method, HTTP route/binder/body,
App-only JSON root, MCP registration and native-response advertisement. The
registered typed request now refuses without owner/content execution. Named
changes and historical Core contracts/readers remain; member-link cardinality
and accepted HRIS application remain unimplemented.

Source review added actual MCP discovery/named retry checks and retained V2
payload/metadata saved-prefix rejection. It also found a stale exact MCP catalog
fixture from earlier batches; its explicit expected tool set was refreshed without
removing authorization or metadata assertions. Live Jev-1.14.0 adverse signals
for a remaining generic writer/public exposure/auth regression were .06/.05/.11;
historical-fixture signals were uncertain (.40/.69), so source and executable
checks remain the acceptance authority. Local assembled acceptance passed as
recorded below; final-head delivery is tracked separately on #1176.

The first frozen full gate then failed an existing RBAC administration-read fixture
on a missing `workforce_people` table. Its in-memory membership/event composition
left native Person display-name enrichment uncontrolled. This fixture boundary
was corrected with a fixture-local null roster-name reader, retaining every
authorization/readability assertion. All 13 RBAC wall cases then passed with Cassie
configured to an unreachable port. The first full run finished 8,706/8,707 with
unchanged inputs. The next fresh native run passed **8,707/8,707**, zero
failures/skips, in 12m47s. All 14 compiler/runtime-fixture input hashes remained
unchanged. This second run supplies current local acceptance; final committed-head
review, hosted CI and merge remain separate delivery gates.


## Person retirement delivered; Program target ordering checkpoint

This checkpoint is paused at the user's request. The Program full run described
below was later canceled; no completed full acceptance is claimed. See the
[restart handoff](restart-handoff.md) before taking further action.

[PR #1187](https://github.com/bdgrz/compliance-core/pull/1187) merged Person
writer retirement as `4e4c9d01`. The merged tree matches reviewed `a3e501f3`;
local, exact-head hosted and independent post-merge gates each passed 8,707 units.
Integration branch readback/push is complete. Person links and accepted HRIS
application remain separate missing behavior.

The next bounded repair compares every present Program target date across unknown
intermediates. Clean RED passed29/failed6, then the six-line repair passed65
focused/native/history/HTTP cases. Scaffold import/style fixes are not RED.
All six date pairs, all16 present/null patterns and equality are covered; unchanged
native handlers still provide original receipt/current authorization behavior.
Strict build/whitespace pass; the assembled full gate is running. The
[review](reviews/program-target-date-ordering.md) records the accepted breaking
qualification for old invalid stored plans, which may stop historical reads and
fresh projection rebuild. No data conversion/deployment is claimed.

Evidence Support withdrawal is held at `3c81d366`, tests/contracts `6307b091`, on
`work/evidence-support-withdrawal`. It is source-authored and unexecuted. Original
assertion reads and new-use eligibility are separate authoritative seams; public
wiring is not enabled. Examinations fixed-cut history, Advisor new-use checks and
Applications/read-status qualification remain before a coherent public workflow.
No new Fitz projection behavior is accepted as the target. The held Access Cassie
slice also has a source-reviewed integration checklist covering shared schema/DI,
real native authorization fixtures, cursor exception doubles and compiled E2E type
references; source review is not executed migration acceptance.
