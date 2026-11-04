# Proposed domain implementation progress

The locked context documents are the specification. This ledger records implemented
behavior and validation; a matching type name or a passing focused test does not
establish completion of an owner, workflow, or context.

Tracking issue: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

## Delivery baseline

- Integration branch: `feat/proposed-domain-impl`.
- Starting commit: `c6d16cdb` (Portia dependency prerequisite, issue #1175).
- Dependency validation: Release build and format passed; 12 focused migration checks
  passed. The first full run exposed processor-wrapper assertion changes. The latest
  run passed 8,048 of 8,053 tests; the five remaining assertions were corrected and
  passed their focused recheck. The full suite is running again. This is not a
  green full-suite baseline yet.
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
| `workforce` | `Features/Workforce/**` in Common, Core, App and feature tests | Start, leave, return and end WorkRelationship lifecycle | Qualified; RED pending |
| `assurance` | `Features/Remediation/**` in Common, Core, App and feature tests | Product Waiver request, independent review and revocation | Qualified; RED pending |
| `methodology` | `Features/MethodologyTemplates/**` in Common, Core, App and feature tests | TemplateVersionApplication per-version entry outcomes | RED commit `badff5d7`; assembled execution pending |

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
| [Assurance work](contexts/assurance-work.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Policies](contexts/policies.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Risk and provider oversight](contexts/risk-and-provider-oversight.md) | Partial: treatment reviewer independence; see existing owner review | Full requirement mapping and workflow validation pending |
| [External access governance](contexts/external-access-governance.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Change and incident oversight](contexts/change-and-incident-oversight.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [System description](contexts/system-description.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Readiness](contexts/readiness.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Advisor collaboration](contexts/advisor-collaboration.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Exports](contexts/exports.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Firm methodology](contexts/firm-methodology.md) | Not mapped | Full requirement mapping and workflow validation pending |
| [Work queue](contexts/work-queue.md) | Not mapped | Full requirement mapping and workflow validation pending |

The existing [owner reviews](README.md#aggregate-implementation-evidence) remain
valid only for the behavior and validation they explicitly record. Commercial
operations are not designed in this proposal; context-specific documented deferrals
remain explicit rather than silently counted as delivered.

## Batch evidence

No proposed-domain batch has been accepted on the integration branch yet. Each
accepted batch will record its requirement rows, behavior, source paths, RED and
GREEN results, deferred wiring, remaining work and validation failures.
