# Organization creation and primary administration

Status: implemented in source on 2026-11-05; controlled feature acceptance passes,
native/full acceptance remains blocked. This is one
capability across API, MCP, write models and read models, not completion of the
Platform access context. Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

## Client-owned identity

The client creates a UUID once and retains it for creation retries. Both HTTP
`POST /api/v1/tenants` and MCP `bdgrz.tenant.register` require `tenant_id`:

```json
{
  "tenant_id": "182f0217-b1fc-43c6-bd4a-d932af42b4d8",
  "name": "Acme",
  "legal_name": "Acme LLC",
  "slug": "acme"
}
```

The authenticated creator must own a verified platform email (developer
session authentication retains its explicit local verification exemption).
The backend never substitutes an organization ID or accepts an invitation to
someone else as the first administrator. Exact creation retries retain the
original result even if the slug later changes. Changed creation terms or a
different creator under the same tenant ID conflict without another fact.

Poll `GET /api/v1/tenants/{tenant_id}/provisioning-status` during asynchronous
bootstrap. A rejected first slug preserves the organization identity; the creator
can request another slug while the organization is still registered. Both
slug-first and primary-first delivery orderings can finish activation.

## Layer coverage

| Layer | Implemented behavior | Executed evidence |
| --- | --- | --- |
| API | Client-ID creation/retry, provisioning, slug recovery, primary designation/release/read and membership/access reads | Real HTTP cases; generated OpenAPI requires `tenant_id` and includes the three primary routes |
| MCP | Same identified creation and public primary operations; internal receivers excluded | Real MCP invocation, required input schema and tool discovery cases |
| Write models | Tenant identity/creation intent; two-seat primary owner; member protection and outcome/release coordination | Focused behavior, valid replay, competing appends and delayed delivery cases |
| Read models | Materialized tenant/membership views, episode/cap/primary fields; current primary and access explanations | Production projector/detail/list/discovery and source query cases |

## Owners and coordination

- **Tenant**, identified by the client-selected tenant ID, owns creation, original
  registration intent, address state and activation. Slug uniqueness remains with
  the independent slug owner.
- **PrimaryOrgAdmins**, identified by tenant ID, owns at most two seats. Pending
  designations consume a seat but do not count as a replacement that permits
  release. A completed primary can be released only while another completed
  primary remains. This small owner grows with primary changes, not ordinary
  administrator churn.
- **Member**, identified by tenant/user and its membership episode, owns the
  primary mark versus suspension/deprovisioning decision atomically. Only an
  active non-guest episode accepts the mark. A refusal answers the reserved
  designation; it is not a generic failed-command event.
- The source reservation triggers member marking; its exact outcome completes
  or refuses the seat. Release commits before unmarking. Delayed deliveries retain
  exact designation/episode tokens, so an old release cannot clear a new mark and
  an old mark retry cannot restore a released designation.
- Portia retains persistence, OCC, reactor checkpointing and retries. Competing
  seat changes append to the same source stream and must reread/redecide on
  conflict. No projected roster decides continuity.

## Authority, reads and public surface

The creator's completed primary designation grants Org Admin directly, independent
of team membership. Evaluation requires the current member mark and matching
completed primary seat. Pending or released seats confer no primary authority.
The ordinary-admin guard is no longer used by the five active member/team
mutation handlers; historical process fixtures remain explicitly test-only.
Primary suspension and deprovisioning are refused on Member itself. Current tenant
and permission checks and the separate Attest authoring wall remain in force.

The public API and MCP expose `DesignatePrimaryOrgAdmin`,
`ReleasePrimaryOrgAdmin` and `GetPrimaryOrgAdmins`. Mark/unmark/outcome receivers
are internal and reject human callers. The primary read view distinguishes
pending and completed seats. Membership detail/list and tenant discovery expose
membership episode and primary designation; guest membership views also retain
program caps. Access explanations expose the primary designation separately from
team paths, and include its organization permissions.

The existing production membership directory remains Fitz-backed. This packet
does not claim a membership-store migration. Existing Cassie team/role directory
paths remain subject to native acceptance. No persisted-data rebuild or deployment
is claimed.

## Evidence and limits

Before implementation, the first five seat tests produced four behavioral failures
and one pass. An earlier scaffold analyzer failure is not behavioral RED.
The user then requested parallel source work with compilation/tests deferred until
the entire chunk was assembled. All agents followed that rule; coordinator
validation follows the frozen assembly.

The [event-name request](organization-bootstrap-event-names-jev-request.json) and
[response](organization-bootstrap-event-names-jev-response.json) record
`jev-1.14.0`: seven approved names received support from .88 to .93. Jev is advisory;
source invariants and executed tests establish acceptance.

A second bounded [source review request](organization-bootstrap-jev-request.json)
and [response](organization-bootstrap-jev-response.json) record nine adverse
questions. Scores were .23 for last-primary release, .05 for a third seat, .17/.18
for late mark/unmark, .10/.07 for invalid outcome/unreleased unmark, .28 for released
primary authority, .30 for circular activation and .04 for server-created identity.
The .23/.28/.30 signals are not clearance: competing-release, stale-mark authority
and real composed activation cases provide the relevant executable evidence.

Native acceptance remains independently tracked in
[#1188](https://github.com/bdgrz/compliance-core/issues/1188); broader assembled
failures are tracked in [#1189](https://github.com/bdgrz/compliance-core/issues/1189).
No hosted CI, merge, release or production qualification is claimed here.

## Final local validation

- Strict Release build: zero warnings/errors; whole-solution whitespace passes.
- Expanded feature selection: **152 passed / zero failed / zero skipped**.
- Real generated HTTP/MCP selection: **8 passed / zero failed / zero skipped**.
- Both proposal/runtime inventory validators and all six Node tooling cases pass.
- Generated OpenAPI export passes the required client tenant-ID and three public
  primary-route checks. Export is a contract check, not native startup acceptance.
- Final backend unit gate: **8,221 passed / 1,080 failed / zero skipped**.
  The 6,753 source/fixture inputs remained unchanged throughout. Failure groups:
  542 transactional DDL;
  479 connection exhaustion;
  59 other assembled failures;
  no new failing test names compared with the previous full checkpoint.
- Initial focused run: 112 passed / 14 failed due to setup events being included
  in new-test assertions. Baselines now compare the exact before/after event set,
  preserving no-new-fact and single-outcome checks. First full run also exposed
  two obsolete expectations (activation workload names and exact creation retry);
  they were corrected and the full gate repeated on the revised inputs.

[Machine-readable evidence](organization-bootstrap-validation.json) retains exact
counts, local log/TRX locations and the frozen input manifest hash. The transport
seam uses real adapters/authorizers with controlled event/KV persistence and manual
worker progression; it does not qualify native database startup or deployment.
No native blocker was bypassed with retries, permissive authorization or skipped
checks. No PR is opened while the required full gate fails.
