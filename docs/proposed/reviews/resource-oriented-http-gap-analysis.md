# Resource-oriented HTTP gap analysis

Status: first-pass source review against the desired design in
[ADR 0011](../../architecture/decisions/0011-resource-oriented-http-over-command-query.md).
Reviewed 2026-11-19. No runtime changes were made.

## Scope and method

This review statically inventoried the 576 typed `MapPortiaGet`, `MapPortiaPut`,
`MapPortiaPost`, and `MapPortiaDelete` registrations in
`src/Compliance.App/Program.cs`, then inspected representative route and
request contracts, `Page<T>`, and the HTTP JSON setup. The route inventory
contains 251 GET, 265 PUT, 56 POST, and 4 DELETE registrations. There are 571
`/api/v1` and 5 `/api/v2` routes; 539 include `{tenant_id}`, 267 include
`{program_id}`, 24 are under `/platform/`, and 187 include `{command_id}`.
343 paths have more than seven segments (including the API prefix), and the
longest has fourteen. These counts are structural indicators, not a semantic
classification of every endpoint.

This inventory does not include custom auth, health, evidence-upload, criteria
authoring, OpenAPI, or MCP mappings. It also does not establish runtime status
codes, headers, client behavior, authorization outcomes, or read-model
consistency. Those need focused contract and behavior review before cutover.

## Findings

| Target rule | Current evidence | Gap |
| --- | --- | --- |
| Flat `/v1/{resources}/{id}` identity roots; tenant selected by `Tenant-Id` header, other owner/scope in body/query | 539 typed routes put `tenant_id` in the path and 267 put `program_id` there. ADR 0009 requires this and makes the route tenant authoritative. For example, teams use `/api/v1/tenants/{tenant_id}/teams/{team_id}`. | **Breaking architectural gap.** Move tenant selection to the authoritative `Tenant-Id` header; keep other owner/scope values in the body or query. Preserve authorization and tenant isolation. ADR 0011 records the desired successor; ADR 0009's security invariants stay in force. |
| Only one subordinate part level; resource IDs identify relationships | 343 routes exceed seven total path segments, with a maximum of fourteen. Team membership uses `/teams/{team_id}/members/{member_id}/{command_id}`; role permission changes use `/roles/{role_id}/permissions/{permission}/assignments/{command_id}`. | **Broad route redesign.** Classify each segment as an independent resource, a subordinate part, a relationship key, or an operation. Preserve genuinely independent history/revision resources; remove action and scope segments from ordinary resource paths. |
| Fixed vocabulary values or IDs in paths; user-defined values stay out | Candidate non-ID parameters include `{email_address}` (5 routes), `{identifier}` (3), and `{slug}` (1). `{permission}` appears twice and is a fixed vocabulary candidate. `{source_kind}`, `{source_family}`, `{relationship_type}`, `{week_of}`, `{revision}`, and `{version}` need case-by-case classification. | **Direct gaps and classification work.** Move user-defined values such as email addresses and slugs out of paths. Confirm vocabulary parameters are closed in code and retain only independently meaningful IDs in paths. |
| `/v1/system/...` for platform resources | 24 typed routes use `/api/v1/platform/...`; all typed paths include the `/api` prefix, and five routes use `/api/v2`. | **Prefix migration.** Map platform resources to `/v1/system/...`, align the version strategy, and inventory custom mappings separately. |
| GET reads; no POST or PATCH | Typed routes include 56 POST registrations and no PATCH. POSTs cover several kinds of operations, including identity flows, previews/read-like operations, and domain writes. Custom auth endpoints also include POST. | **Verb and intent review.** Map each POST to GET for reads or to an identity-addressed PUT/DELETE operation when appropriate. Some auth or upload flows may need resource contracts of their own; the target does not currently define exceptions. |
| PUT creates at client-minted resource ID; identical retry is accepted; changed content conflicts | Some existing resource roots already use PUT with caller-visible IDs, such as `DefineTeam` at `/teams/{team_id}` and `CreateProgram` at `/programs/{program_id}`. Other creates use POST, and many mutations carry `{command_id}`. | **Partial alignment, behavior unproven.** Keep resource IDs at flat roots, select tenant via header, move other owner/scope values into the body, and verify identical-retry and changed-body conflict behavior at aggregate boundaries. Decide whether command IDs represent resources or should leave the route. |
| PUT fields/status and DELETE relationships; successful writes have no body | Many PUT paths encode operations such as `/renames/{command_id}`, `/deletions/{command_id}`, `/assignments/{command_id}`, `/approvals/{command_id}`, and `/revisions/{command_id}`. The four typed DELETE routes include a relationship removal returning a result type. Some typed PUT/POST mappings also declare response types; actual wire behavior depends on their Portia result configuration. | **Response and lifecycle normalization.** Move scalar/part changes to the part path, lifecycle changes to `/status`, and represent relationship identity directly. Inspect each mapping's actual HTTP result and make successful writes bodyless, preserving `201` plus `Location` for first-time creates. |
| One route maps to one request type; command intent and event facts stay separate | The typed Portia registrations name one request type per route (for example, `DefineTeam`, `AssignTeamMember`, and `RemoveRolePermission`). | **Good structural fit.** Preserve this boundary as routes are consolidated. Avoid introducing multiple commands behind a route to get transition-specific naming. Verify the final generated OpenAPI still describes one request contract for each route. |
| Lists use optional owner/scope filters and return all authorized results without a filter, within the selected tenant | Current tenant-owned lists are generally rooted under `/tenants/{tenant_id}`; examples include `/teams` and `/programs`. | **Behavioral and authorization gap.** Select tenant context from `Tenant-Id`, add query filters for additional scope, and define no-filter reads across everything the caller may read in that tenant. Replace route-scoped authorization with equivalent header/query authorization and cross-tenant proofs for filtered and unfiltered reads. |
| Collections are pages with cursor and limit | `Compliance.Common.Page<T>` contains `Items` and `NextCursor`, but no limit. The API also has collection endpoints typed as `IReadOnlyList<T>`, including service-engagement lists and histories. | **Partial/uneven pagination contract.** Standardize collection responses and inputs on cursor plus bounded limit. Decide whether history collections follow the same pagination contract. |
| ETag/If-Match or `expected_revision` for concurrency | The source search found no explicit HTTP `ETag`/`If-Match` handling in `src`. Explicit `expected_revision` body binding exists in a small number of custom HTTP adapters, while revision expectations also appear in request types. | **Unsettled contract.** Choose and implement the framework-supported header path or consistently carry `expected_revision`; define 428/412 (or the fallback conflict result) for every conditional mutation. |
| Snake-case fields; UUID `*_id`; external keys `*_reference`; `_at`/`_on` suffixes | HTTP JSON is configured with `JsonNamingPolicy.SnakeCaseLower`, and route IDs commonly use `*_id`. Existing date-only contract fields include names such as `FromDate` and `ThroughDate`, which serialize as `from_date` and `through_date`. | **Partial alignment.** Keep snake-case and UUID ID conventions. Rename date-only fields to `_on` at the wire boundary. Inventory external references and timestamp/date fields across request and response schemas before freezing OpenAPI. |
| Money is a decimal string | No decimal-valued business properties were found in the inspected app/common/core source search. The `G29DecimalJsonConverter` found in Core is used by authored-intent digesting and writes JSON numbers, not a discovered money HTTP contract. | **No current contract assessed.** Apply the decimal-string rule to any money fields introduced or found in the complete schema inventory; do not infer an existing HTTP money representation from the digest converter. |

## Contract-specific notes

### Teams and membership identity

The current team root is already a PUT at a caller-addressed `{team_id}`, and
`ListTeams` returns `Page<TeamView>`. However, the route is tenant-nested and
`DefineTeam` contains both `TenantId` and `TeamId`. Membership is addressed by
`member_id`, and `TeamMemberView.UserId` is nullable. The target example uses
`user_id` as the relationship key. These identities are not safely
interchangeable: before adopting the example literally, decide how invited or
not-yet-linked members fit the relationship contract.

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

No tests or runtime gates were run for this documentation-only review.
