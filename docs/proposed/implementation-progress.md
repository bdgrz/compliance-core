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
| `workforce` | `Features/Workforce/**` in Common, Core, App and feature tests | Start, leave, return and end WorkRelationship lifecycle | Local assembled GREEN; hosted batch acceptance pending |
| `assurance` | `Features/Remediation/**` in Common, Core, App and feature tests | Product Waiver request, independent review and revocation | Local assembled GREEN; hosted batch acceptance pending |
| `methodology` | `Features/MethodologyTemplates/**` in Common, Core, App and feature tests | TemplateVersionApplication per-version entry outcomes | Local assembled GREEN; hosted batch acceptance pending |

Workers submit separate RED and GREEN commits. The coordinator observes focused
RED before applying GREEN, wires the assembled batch, runs the required checks,
and records the exact result. Blocked work is parked with a named missing condition
while independent work continues.

## Context coverage

“Not mapped” means the complete reviewed requirements have not yet been reconciled
against executable behavior and tests. It does not mean that the context has no code.

| Reviewed context | Implementation evidence | Remaining work / wiring |
| --- | --- | --- |
| [Platform access](contexts/platform-access.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Firm services](contexts/firm-services.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Program and scope](contexts/program-and-scope.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Workforce assurance](contexts/workforce-assurance.md) | Partial: ended work relationships and retired service identities are terminal; see existing owner reviews | Full requirement mapping and workflow validation pending |
| [Application inventory](contexts/application-inventory.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Technology and information inventory](contexts/technology-and-information-inventory.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Examinations](contexts/examinations.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Auditor collaboration](contexts/auditor-collaboration.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Commitments](contexts/commitments.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Criteria](contexts/criteria.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Control environment](contexts/control-environment.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Evidence](contexts/evidence.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Retention](contexts/retention.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Assurance work](contexts/assurance-work.md) | Product Waiver owner, handlers, authorization and point read locally validated | Finding/CorrectiveAction proposed migration and waiver consumers remain |
| [Policies](contexts/policies.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Risk and provider oversight](contexts/risk-and-provider-oversight.md) | Partial: treatment reviewer independence; see existing owner review | Full requirement mapping and workflow validation pending |
| [External access governance](contexts/external-access-governance.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Change and incident oversight](contexts/change-and-incident-oversight.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [System description](contexts/system-description.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Readiness](contexts/readiness.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Advisor collaboration](contexts/advisor-collaboration.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Exports](contexts/exports.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Firm methodology](contexts/firm-methodology.md) | Per-version outcomes and committed-source opening locally validated | Draft/line migration, entry materialization and parent completion remain |
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
Hosted batch CI and final-head review remain pending.

### Batch 1 implemented behavior and remaining work

| Slice | Implemented and locally tested | Wired centrally | Explicit remaining work |
| --- | --- | --- | --- |
| WorkRelationship (WF-01/WF-03) | Start, leave, return and end decisions; named facts; revision and actor/date/reason handling; primary and observation projections; leaver/unowned consumer status; all four named facts replayed | Four handlers, JSON, HTTP/MCP actions and native response contracts | Manager/department/sponsor/date-correction commands; initial-record and source-reconciliation conformance; complete workflow and competing-append coverage |
| Product Waiver | Exact immutable rule/record scope; request, independent approval/rejection, computed expiry and reasoned revocation; terminal retries; point read and permission/transport guards | Four handlers, authorizer/reader, JSON, role grants, HTTP; request/read MCP only, personal review/revoke HTTP only | Control-evaluation and data-flow waiver consumption; explicit competing-append proof; remaining Finding/CorrectiveAction specification |
| TemplateVersionApplication (FM-03/FM-05) | One immutable entry set per applied version; one created/proposed/refused outcome per entry; exact completion counts; replay; committed application source verification and refusal; failed opening checkpoint retention | Two internal handlers, System-only authorizer, opening reactor and JSON; no public routes | Entry materialization and refusal choreography; parent completion; application identity/order migration; draft/line migration and removal of superseded contribution behavior; explicit competing-append proof |

### Queued work and validation failures

- Finding severity (AW-07): 22 focused RED cases executed, 14 intended failures and
  eight passes. Implementation is frozen in the assurance worktree; GREEN validation
  is queued. Four external Finding test inputs need coordinator updates. Other
  domains retain their own severity vocabulary.
- Remaining named WorkRelationship changes: RED candidate authored; behavioral RED
  not yet observed because its compiler run was cancelled for memory contention.
  Compliance Lead's default manager-chain read grant also needs the approved
  M0-D06 shared-role alignment.
- MethodologyTemplateDraft: RED candidate authored; behavioral RED not yet observed
  after the same compiler cancellation. Candidate implementation is outside the
  project and has not been accepted.
- Four concurrent compiles saturated the 24 GB machine's memory. Coding remains
  parallel in exclusive worktrees; compiler-heavy checks now use one coordinator
  queue with unchanged analyzer/test enforcement. Cancelled runs are not passes or
  behavioral failures.
- Isolated broker assertion failures remain parked in [#1178](https://github.com/bdgrz/compliance-core/issues/1178).

No context is marked complete. The next accepted batch must retain requirement
mapping, actual RED/GREEN evidence, deferred wiring, remaining work and validation
failures rather than replacing them with name or test-count assertions.
