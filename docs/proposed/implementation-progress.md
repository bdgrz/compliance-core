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

## Active batches and exclusive ownership

The coordinator owns registrations, serialization contexts, composition roots,
shared catalogs and fixtures, and all audit/coverage updates. Workers report exact
wiring requirements and edit only their assigned business slice.

| Worker worktree | Exclusive business paths | First bounded batch | State |
| --- | --- | --- | --- |
| `workforce` | `Features/Workforce/**` in Common, Core, App and feature tests | Start, leave, return and end WorkRelationship lifecycle | Batch 1 merged; batch 2 source prepared |
| `assurance` | `Features/Remediation/**` in Common, Core, App and feature tests | Product Waiver request, independent review and revocation | Batch 1 merged; batch 2 source prepared |
| `methodology` | `Features/MethodologyTemplates/**` in Common, Core, App and feature tests | TemplateVersionApplication per-version entry outcomes | Batch 1 merged; batch 2 source prepared |

Workers submit test-first and implementation commits. Following the user's updated
instruction, they now use source review and live Jev before any compilation or test
execution. The coordinator accumulates completed source slices, wires one assembled
batch, then runs the required checks. Authored tests are not recorded as observed
RED, and prepared source is not recorded as validated GREEN. Blocked work is parked with a named missing condition
while independent work continues.

## Context coverage

“Not mapped” means the complete reviewed requirements have not yet been reconciled
against executable behavior and tests. It does not mean that the context has no code.

| Reviewed context | Implementation evidence | Remaining work / wiring |
| --- | --- | --- |
| [Platform access](contexts/platform-access.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Firm services](contexts/firm-services.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Program and scope](contexts/program-and-scope.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Workforce assurance](contexts/workforce-assurance.md) | Partial: named WorkRelationship lifecycle/changes and computed unsponsored gap; retired service identities are terminal | Full requirement mapping and workflow validation pending |
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
zero warnings/errors and all 797 affected business tests passed. Full-suite and
format acceptance remain pending; the table distinguishes earlier RED evidence
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
  rerun is pending. No production guard or assertion was weakened.

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
