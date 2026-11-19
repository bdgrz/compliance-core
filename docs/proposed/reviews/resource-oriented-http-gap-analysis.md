# Resource-oriented HTTP gap analysis

Status: source-level inventory against the desired design in
[ADR 0011](../../architecture/decisions/0011-resource-oriented-http-over-command-query.md).
Reviewed 2026-11-19 with three parallel route-family passes. No runtime changes
were made.

## Scope and method

The three route passes inventoried all 576 typed Portia registrations in
`src/Compliance.App/Program.cs` (197, 174, and 205 by source slice), plus 14 more in
`Features/Criteria/CriteriaAuthoringEndpoints.cs`. The typed total is 590:
254 GET, 276 PUT, 56 POST, 4 DELETE, and no PATCH. Seven additional direct
custom mappings are recorded separately: three auth routes, one development
evidence upload route, and three health probes. Generated Portia OpenAPI and
MCP transport endpoints are excluded as framework surfaces.

The typed routes include 585 `/api/v1` and 5 `/api/v2` paths; 539 include
`{tenant_id}`, 267 include `{program_id}`, 38 use `/platform/`, and 187
include `{command_id}`. 347 paths exceed seven total segments, including the
API prefix; the maximum is fourteen. These are structural signals, not a claim
that every deep path or every operation ID is invalid. The full row-level
inventory records source line, verb, request/result type, route parameters,
depth, and review flags in
[resource-oriented-http-route-inventory.csv](resource-oriented-http-route-inventory.csv).
Its declared result-type column reflects source-level Portia mapping types; it
does not infer final wire status or body where endpoint configuration
transforms native results. Review flags are triage signals, not automatic
semantic verdicts.

The review inspected representative request types and handlers, `Page<T>`,
custom HTTP binding, and JSON configuration. It does not establish every
runtime status/header, authorization outcome, client behavior, or read-model
consistency. Those require focused contract and behavior review before cutover.

## Findings

| Target rule | Current evidence | Gap |
| --- | --- | --- |
| Flat `/v1/{resources}/{id}` roots; tenant selected by `Tenant-Id`; other owner/scope in body/query | Of 590 typed routes, 539 put `tenant_id` in the path and 267 put `program_id` there. ADR 0009 makes the route tenant authoritative. Team and program examples are `/api/v1/tenants/{tenant_id}/teams/{team_id}` and `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}`. No `Tenant-Id` handling was found in the app source search. | **Breaking architectural gap.** Move tenant selection to the authoritative header; keep other owner/scope values in body or query. Replace the current route-based authorization selector without weakening tenant isolation. |
| Only one subordinate part level; own-ID resources have flat roots | 347 typed paths exceed seven segments, a structural signal. Independently identified items are nested, including system instances under applications, auditor responses under requests, and corrective actions under findings. Client-service and boundary creates are program-nested, while reads and changes use tenant-root routes. | **Broad route redesign and inconsistent roots.** Classify each segment as identity, scope, part, relationship, lifecycle, or operation. Preserve genuinely separate history resources and use flat collection roots for independently identified resources. |
| Fixed vocabulary values or IDs in paths; user-defined values stay out | Definite user-defined path values include `{email_address}` (5 routes), `{slug}` (1), and criteria `{code}` (1). `{identifier}` occurs 5 times; one control-draft route derives request identity from tenant, program, and that identifier. Dynamic `{revision}` (7), `{version}` (3), `{package_version}` (5), `{rule_version}` (1), and `{week_of}` (2) need redesign or explicit identity classification. `{permission}`, `{relationship_type}`, criteria `{field}`, and `{category}` are code-whitelisted vocabulary values. | **Direct gaps plus classification.** Remove email, slug, and arbitrary code values from paths. Convert independently addressable versions to UUID `*_id`; keep only values confirmed as fixed vocabularies. |
| `/v1/system/...` for platform resources | 38 typed routes use `/api/v1/platform/...`, including 14 criteria-authoring routes outside `Program.cs`. Five other typed routes use `/api/v2`. | **Prefix migration.** Map platform resources to `/v1/system/...`, settle the five v2 routes against the v1 target, and account for the `/api` prefix across all 590 typed routes. |
| GET reads; no POST or PATCH | Typed routes include 56 POST registrations and no PATCH. POSTs cover several kinds of operations, including identity flows, previews/read-like operations, and domain writes. Custom auth endpoints also include POST. | **Verb and intent review.** Map each POST to GET for reads or to an identity-addressed PUT/DELETE operation when appropriate. Some auth or upload flows may need resource contracts of their own; the target does not currently define exceptions. |
| PUT uses caller-minted resource/relationship identity for natural idempotency | 187 routes contain `{command_id}`. In 182 request types the explicit `IIdentifiedRequest.RequestId` maps to `CommandId`; `SetWorkDigestPreference` exposes the same mapping through a public `RequestId`; four more command-bearing request types do not implement that interface. Other userland patterns include `CreatePolicyDraft` using `PolicyId`, `RequestWaiver` using `WaiverId` on a POST collection route, and `RecordWorkRelationship` deriving an ID from tenant plus `SourceWorkerId`. | **Direct userland design gap.** Portia's interface does not force a separate command ID. Make resource or relationship identity the default retry identity. Audit each operation to distinguish a true operation resource from a retry token; keep internal IDs where needed without exposing a second path ID by default. |
| PUT fields/status and DELETE relationships; successful writes have no body | Action suffixes include suspension, reinstatement, deprovisioning, revise, approve, retire, close, and relationship assignment/removal. Of four typed DELETEs, tenant reactivation is a lifecycle action, one relationship removal declares a result type, and one role-permission removal retains a `/removals/{command_id}` suffix. 41 PUT mappings declare `OperationAcceptedResponse`; other writes declare registration/view result types. | **Response and lifecycle normalization.** Replace action suffixes with desired-state `/status`, named part PUTs, or relationship PUT/DELETE. Inspect each Portia result configuration; writes must be bodyless and create outcomes must use `201` plus `Location` on first create. |
| One route maps to one request type; command intent and event facts stay separate | The typed Portia registrations name one request type per route (for example, `DefineTeam`, `AssignTeamMember`, and `RemoveRolePermission`). | **Good structural fit.** Preserve this boundary as routes are consolidated. Avoid introducing multiple commands behind a route to get transition-specific naming. Verify the final generated OpenAPI still describes one request contract for each route. |
| Lists use optional owner/scope filters and return all authorized results without a filter, within the selected tenant | Current tenant-owned lists are generally rooted under `/tenants/{tenant_id}`; examples include `/teams` and `/programs`. | **Behavioral and authorization gap.** Select tenant context from `Tenant-Id`, add query filters for additional scope, and define no-filter reads across everything the caller may read in that tenant. Replace route-scoped authorization with equivalent header/query authorization and cross-tenant proofs for filtered and unfiltered reads. |
| Collections are pages with cursor and bounded limit | 103 typed GET mappings declare `Page<T>`; that type contains `Items` and `NextCursor`. Nine GETs return `IReadOnlyList<T>`. Other collection-bearing results include custom pages and unpaged composite views such as work queues, access expectations, and primary administrators. Some list requests accept `Limit` and `Cursor`; the contract is uneven. | **Partial/uneven pagination contract.** Ensure every collection has bounded `limit` and cursor semantics, including histories and collection-bearing composite views. Preserve useful page metadata while standardizing the wire shape. |
| ETag/If-Match or `expected_revision` for concurrency | The source search found no explicit HTTP `ETag`/`If-Match` handling in `src`. Explicit `expected_revision` body binding exists in a small number of custom HTTP adapters, while revision expectations also appear in request types. | **Unsettled contract.** Choose and implement the framework-supported header path or consistently carry `expected_revision`; define 428/412 (or the fallback conflict result) for every conditional mutation. |
| Snake-case fields; UUID `*_id`; external keys `*_reference`; `_at`/`_on` suffixes | HTTP JSON uses `JsonNamingPolicy.SnakeCaseLower`. Current mismatches include `RecordWorkRelationship.SourceWorkerId` as `source_worker_id` for a source-system key and `DateOnly` fields `StartDate`/`EndDate` as `start_date`/`end_date`; other examples include `FromDate` and `ThroughDate`. `primary_org_admins` also uses underscores in the path. | **Partial alignment.** Keep snake-case JSON and UUID IDs, rename date-only wire fields to `_on`, external keys to `*_reference`, and path words to the agreed hyphenated form. Inventory all DTOs before freezing OpenAPI. |
| Money is a decimal string | No decimal-valued business properties were found in the inspected app/common/core source search. The `G29DecimalJsonConverter` found in Core is used by authored-intent digesting and writes JSON numbers, not a discovered money HTTP contract. | **No current contract assessed.** Apply the decimal-string rule to any money fields introduced or found in the complete schema inventory; do not infer an existing HTTP money representation from the digest converter. |

## Route-family inventory

| Route family | Current patterns and examples | Inconsistency to resolve |
| --- | --- | --- |
| Tenant access, teams, and roles | Tenant suspension/offboarding, member suspension/reinstatement/deprovisioning, teams, roles, permissions, and primary-admin routes live under `/api/v1/tenants/{tenant_id}`. Team membership uses `/teams/{team_id}/members/{member_id}/{command_id}`; permission assignment/removal uses `/roles/{role_id}/permissions/{permission}/.../{command_id}`. | Move tenant selection to the header. Keep a relationship path only when its identity is the other resource's ID. Replace action suffixes with desired-state PUT/DELETE. The team example's `user_id` differs from current `member_id`; current `TeamMemberView.UserId` is nullable, so invited/unlinked members need an identity decision. |
| Programs and compliance records | Controls, commitments, policies, campaigns, risks, readiness, findings, audit requests, and access reviews generally nest beneath `/tenants/{tenant_id}/programs/{program_id}`. Examples include control draft revision/approval, finding closures/corrective actions, and auditor-request responses. | Move tenant and program ownership out of routes. Give each independently identified record a flat collection root; keep only one-level parts/relationships below it. Preserve revisions, approvals, and reviews as domain facts without making a generic command ID the default URL identity. |
| Work and workforce | Work-item claims, assignments, delegations, and escalations append `{command_id}`. Person detail and membership correlation are command-specific paths. Work relationships use POST and start/leave/end or manager/department/sponsor action paths; service identities use POST for creation and POST action paths for owner/purpose/disable/enable/retire. | Use caller-owned resource or relationship IDs; represent replaceable fields at part paths and lifecycle as `/status`. `RecordWorkRelationship` currently has no relationship ID in its request and derives one from `TenantId` plus `SourceWorkerId`; its source-system key should also follow the `*_reference` convention. `RecordServiceIdentity` has no ID field and uses `context.RequestId`. |
| Applications and inventory | System instances are nested under applications despite their own IDs. Client services and boundaries are nested beneath programs on create, then use tenant-root paths on reads/changes. Application relationships have three forms: POST and DELETE at `/applications/{application_id}/relationships/{relationship_id}`, POST at `/applications/{source_application_id}/relationships`, and DELETE at `/applications/{source_application_id}/relationships/{relationship_type}/{target_application_id}`. | Flatten independently identified resources and make each relationship use one identity model. The POST/DELETE pair on the same relationship URL should become PUT/DELETE with no body on delete. The collection POST has no `RelationshipId`; relationship type is allowlisted, while the target application ID is a candidate relationship key. |
| Evidence, retention, audit, and examination | Evidence-support links/withdrawals, artifact retention bases/holds, audit-package version actions, snapshots, examination assertions/outcomes, and period-close actions are deeply nested. `IssueArtifactDownload` records an attributable download; quarantine release records an attributed fact. | Classify each item as an independent resource, relationship, field, lifecycle state, or immutable fact. Model download and quarantine-release facts without turning a side effect into a read. Several POST creates already have candidate IDs in paths or request data and can be reviewed for PUT semantics. Audit-package and examination actions also include non-UUID version/key path values. |
| Criteria authoring | Fourteen typed routes in `CriteriaAuthoringEndpoints.cs` use `/api/v1/platform/criteria-editions/{edition_id}/...`; writes use operation or attempt IDs and return `CriteriaOperationReceipt`, with GET routes for receipts. | Platform routes need `/v1/system`. Receipts are durable operation resources with their own IDs, but currently nest under editions and action names; flatten or explicitly classify their identity. `{field}` and `{category}` are fixed vocabularies; `{code}` is arbitrary bounded text and cannot stay in a path. |
| Authentication, upload, and health | Custom routes include `/auth/config`, `/auth/session`, `/auth/logout`, development-only multipart POST `/api/v1/tenants/{tenant_id}/evidence-artifacts`, and `/health/live`, `/health/ready`, `/healthz`. Typed identity flows add four POST routes. | Health probes are deliberate infrastructure exceptions. Decide how the blanket no-POST rule applies to auth protocol flows and binary uploads. The upload currently has no caller-minted artifact ID and returns `EvidenceCapture` with the new ID; streaming may need an explicit exception or an artifact-addressed PUT. Auth routes are hidden from OpenAPI, so their contract needs its own inventory. |

## Contract-specific notes

### Teams and membership identity

The current team root is already a PUT at a caller-addressed `{team_id}`, and
`ListTeams` returns `Page<TeamView>`. However, the route is tenant-nested and
`DefineTeam` contains both `TenantId` and `TeamId`. Membership is addressed by
`member_id`, and `TeamMemberView.UserId` is nullable. The target example uses
`user_id` as the relationship key. These identities are not safely
interchangeable: before adopting the example literally, decide how invited or
not-yet-linked members fit the relationship contract. `AssignTeamMember`
documents `CommandId` as the assignment's logical identity for exact retry.
The target should decide whether the current membership or a distinct
assignment is the public resource identity, then let the request identity
follow that choice instead of adding both `member_id` and a generic
`command_id` to the route.

### Write then GET behavior

ADR 0011 makes the client read the resulting representation with GET after a
write. This route review does not establish that a Portia.Cassie read model is
updated before that GET, nor whether current write response bodies compensate
for projection delay. A follow-up must define the read-after-write guarantee,
including how the API responds while an asynchronous projection has not caught
up.

### Security and compatibility

ADR 0009 currently depends on a tenant path value and requires that value to
win over any conflicting body tenant ID. The target replaces the path value
with an authoritative `Tenant-Id` header and removes duplicate `tenant_id`
values from the request body/query. Authorization must validate that header on
every tenant-scoped request. Additional scope filters must be checked against
that selected tenant; unfiltered lists cover all resources the caller may
read within it. Background, MCP, and HTTP request paths must retain consistent
tenant context. The header must also be allowed and forwarded by browser CORS,
ingress, and gateway configuration.

`WorkAssignmentHttp` currently resolves tenant, program, work-item, and command
identities from route values. Its OpenAPI body records only expected revision
and action fields, while the binder checks any extra identity properties in
the JSON body and rejects conflicting values. Replace this route-canonical
binding with the target header/resource binding; keep mismatch checks only for
values that remain in an explicitly documented contract.

The route inventory is backend-only. A cutover plan still needs the generated
OpenAPI diff, the supported client repositories, deployment compatibility
windows, and old-route retirement criteria. This review does not claim those
consumers have been inspected.

## Suggested implementation sequence

1. Classify the resource roots and determine which existing nested records have
   independent identity. Resolve special cases such as team-member identity,
   history, import, upload, and authentication resources.
2. Replace ADR 0009's path-authority and route-layout clauses with
   `Tenant-Id` header selection and body/query checks for additional ownership
   and scope, preserving its tenant-isolation guarantees.
3. Define canonical request/response schemas and error/status mappings for
   create retry, conflict, lifecycle transition, pagination, and concurrency.
4. Migrate route families, regenerate OpenAPI, and update client integrations
   in a coordinated version/cutover plan.
5. Add focused contract and cross-tenant proofs for the new routes, including
   no-filter list behavior and write-then-GET consistency.

No tests or runtime gates were run as part of this source-level inventory.
