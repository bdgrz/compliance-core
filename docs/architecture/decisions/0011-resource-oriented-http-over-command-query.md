# Resource-oriented HTTP over a command/query core

Status: desired API design; see the
[current-route gap analysis](../../proposed/reviews/resource-oriented-http-gap-analysis.md).
Recorded 2026-11-19.

This document captures the target HTTP contract for Compliance Core. It is a
breaking wire-contract direction. It does not claim that current routes,
request contracts, handlers, OpenAPI output, or clients already conform.

## Decision

### Resource identity and paths

- A resource with its own identity is addressed at the collection root:
  `/v1/{resources}/{resource_id}`. Its identifier is the client's stable,
  opaque ID.
- Only subordinate parts without independent identity are nested, at most one
  level below their owner. Scalar or structured fields use named paths such as
  `/name`, `/contact-details`, and `/status`. Relationships keyed by another
  resource use that resource's ID, for example
  `/members/{user_id}` and `/role-assignments/{role_id}`.
- Platform-level resources live under `/v1/system/...`.
- A path parameter is either an ID or a value from a vocabulary fixed in code.
  Vocabulary-valued parameters are named for their value, such as
  `{permission}`. User-defined codes, names, slugs, and labels never identify
  path segments.
- Paths do not encode owner or scope. Route templates use lowercase
  hyphen-separated words; interpolated parameter names and all JSON names use
  `snake_case`.

### Owner and scope

- Tenant-scoped requests carry the selected `tenant_id` in the `Tenant-Id`
  HTTP header. The header is the sole tenant selector for the request; do not
  repeat that `tenant_id` in the route, query, or write body. Treat the header
  as untrusted input and authorize it on every request. Missing or invalid
  tenant context must fail closed; its exact HTTP error mapping is part of the
  API contract work.
- Create bodies carry their owner or scope, for example
  `{"scope_kind":"company","scope_id":"…"}` or
  `{"application_id":"…"}`.
- Collection reads select optional owner/scope filters in the query string,
  for example
  `GET /v1/teams?scope_kind=company&scope_id=…`. With no additional scope
  filter, a list returns every resource the caller may read in the selected
  tenant.
- Owner and scope values remain untrusted input: request authorization and
  domain ownership checks must verify them for every request. The header
  replaces path-carried tenant selection; it does not weaken tenant isolation.

### Verbs and responses

| Method | Use | Success |
| --- | --- | --- |
| `GET` | Read a resource, subordinate part, or page | `200` with the representation |
| `PUT /v1/{resources}/{id}` | Create a resource under a client-minted ID | First create: `201` with `Location`; identical repeat: `200` or `204`; changed body at the same ID: `409` |
| `PUT .../{part}` | Replace one subordinate part | `204` |
| `PUT .../status` | Request a lifecycle transition with `{"status":"…"}` | `204`; disallowed transition: `409` |
| `DELETE` | Remove or end a resource/relationship idempotently | `204`, including an identical repeat |

`POST` and `PATCH` are not used. Successful writes return no representation
body; clients read the result through `GET`. A write response may include
protocol headers such as `Location` and `ETag`.

Each route maps to exactly one request type. A command names the requested
intent; recorded events name the facts. For example,
`ChangeCompanyStatus` accepts the target status and the aggregate records the
corresponding `CompanySuspended` or `CompanyReactivated` fact. Do not split one
route across multiple commands merely to encode transition-specific names.

### Caller-owned identity and idempotency

- The caller mints resource IDs. A retried `PUT` therefore addresses the same
  identity and is naturally idempotent.
- A bootstrapper that needs repeatable identity derives the ID from the
  owner's identity, never from a mutable or non-unique name. For example, the
  administrators team ID derives from `(scope_kind, scope_id)`.
- The aggregate creates under the supplied ID, accepts an identical repeat,
  and rejects a different body at that ID with a conflict. It does not police
  how the caller derived the ID.
- Request tracking should reuse the caller-minted resource or relationship ID
  when one exists. Do not require a separate `command_id` in the URL solely to
  make a write retryable. If internal processing needs a distinct operation
  ID, keep it separate from public resource identity and expose it as a route
  identity only when the operation is itself an independently addressable
  resource.
- Names and codes are labels that may change and need not be unique. If a
  business rule requires uniqueness, enforce it through an explicit
  authoritative record. Facts that protect behavior, such as `is_built_in`,
  are persisted state and are checked by authorization.

### Wire shapes and concurrency

- JSON property names are `snake_case` throughout.
- Bdgrz IDs are UUIDs named `*_id`. Keys owned by another system are named
  `*_reference`.
- Timestamps end in `_at`; calendar dates end in `_on`.
- Money is a decimal string, represented as `type: string, format: decimal`.
- Collections are pages with an opaque `cursor` and a bounded `limit`.
- Concurrency uses `ETag` and `If-Match` where the framework supports them:
  missing preconditions return `428`, stale versions return `412`. Where that
  facility is unavailable, contracts carry `expected_revision`.

### Teams example

Every tenant-scoped request below includes `Tenant-Id: …`.

```http
GET    /v1/teams?scope_kind=&scope_id=
GET    /v1/teams/{team_id}
PUT    /v1/teams/{team_id}                     {"name":"…","scope_kind":"…","scope_id":"…"}
PUT    /v1/teams/{team_id}/name                {"name":"…"}
PUT    /v1/teams/{team_id}/status              {"status":"retired"}
GET    /v1/teams/{team_id}/members
PUT    /v1/teams/{team_id}/members/{user_id}
DELETE /v1/teams/{team_id}/members/{user_id}
```

## Relationship to earlier decisions

When this target is adopted for runtime routes, it supersedes the route-layout
and path-carried tenant-selection clauses in
[ADR 0009](0009-tenant-identity-federation-and-context.md). ADR 0009's authorization, tenant-isolation,
identity, and context-propagation requirements remain in force. The
`Tenant-Id` header replaces `tenant_id` in the route; resource owner and
additional scope stay in the body or query. The gap analysis must identify
every route and request contract affected, including how header-selected
tenant context and body/query scope are authoritatively verified and how
unfiltered reads remain limited to resources the caller may read in that
tenant.

## Consequences

- Existing `/api/v1/...` routes and path-scoped routes are not implicitly
  compatible. Route and client migrations require an explicit cutover plan.
- Resource IDs, the `Tenant-Id` header, owners, scopes, and command identities
  must be bound exactly once at the transport boundary and passed to the one
  selected request type.
- HTTP and MCP continue to dispatch through the same transport-independent
  request, authorization, guard, and handler pipeline.
- The target does not relax server-defined authorization, lifecycle rules,
  event-sourced history, or authoritative ownership checks.
