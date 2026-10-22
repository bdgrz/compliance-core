# Role-permission operations

[#1032](https://github.com/bdgrz/compliance-core/issues/1032) delivers the
RolePermission portion of ACCESS-08 under [#1022](https://github.com/bdgrz/compliance-core/issues/1022).
TeamRole, TeamMember, Member and PlatformOperatorRoster remain separate slices.
The existing tuple owner remains the authority for one active permission
assignment. It now distinguishes each assignment episode and each mutation.

## Contract and authority

Assignment requires `CommandId` and `ExpectedRevision`. Removal additionally
requires `AssignmentId`, the command identity of the assignment being removed.
The required operation identity is bound by these routes:

- `PUT /api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}/assignments/{command_id}`
- `DELETE /api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}/removals/{command_id}`

PUT takes `expected_revision` in JSON. DELETE takes `expected_revision` and
`assignment_id` in the query, following Portia's DELETE binding contract.
The body need not repeat route values; conflicting duplicates are rejected.
MCP exposes the required identities as ordinary typed command arguments.
Missing, malformed and empty command identities are rejected. No generated
server identity substitutes for a client command identity.

Portia 0.9.0 binds typed identity to logical request metadata across dispatch
and serialized transports, while execution attempts retain their own identity.
The linked prerequisite is [cntryl/portia#168](https://github.com/cntryl/portia/pull/168).
These commands retain their existing transport declarations; the slice adds no
queue worker or remote listener. The concrete queue-envelope regression uses
an explicit test catalog and the application's generated serializer options.

Authentication and the existing system-owned catalog authorizer run before
hydrating a selected prior operation. User and MCP member callers remain denied
permission mutations. The two protected administrator permissions retain their
removal barrier, including normalized spelling and surrounding whitespace.

## Accepted facts and retries

The canonical owner is `(TenantId, RoleId, normalized Permission)`. Commands select
native accepted facts by `CommandId` inside that owner. Each assigned/removed fact
retains its operation identity, original expected revision and credential-free
actor provenance; removal also retains the exact assignment episode.
Replay validates the owner, canonical permission, original revision and episode.

An exact retry compares the selected accepted fact before checking current state
or current revision. It returns success without appending. Old assignment retries
cannot restore removed authority, and old removal retries cannot remove a newer
assignment. Reusing an accepted identity with a different operation kind, actor,
original revision or removal episode conflicts. A new operation enforces the
expected revision and active assignment episode. A distinct new assignment while
already assigned conflicts; it cannot establish unrecorded successful intent.

The aggregate retains current episode state and at most one selected fact. There
is no receipt event, lifetime receipt dictionary or lifetime operation cap.
Physical hydration still reads the complete tuple history; Portia also maintains
its replay event identities. This change does not make physical replay constant
in history length.

## Reactor structure and recovery

TenantRbacBootstrap owns all four built-in roles, teams and links and all 36
existing default grants. The twelve superseded role-catalog/permission backfill
reactors and their registrations have been removed. This is a direct pre-release
cutover, with no compatibility replay or environment reset performed.

Creator membership, the administrator role link and the three critical grants
precede the team-member assignment that triggers tenant activation. Invitation
bootstrap continues to leave membership to the invitation workflow. Per-tenant
activation remains independently recoverable.

Each grant effect derives its command identity from the immutable source event,
effect kind, target owner and initial lifecycle, and retains expected revision
zero. Replaying a partially committed source resolves its already accepted native
facts, including a grant deliberately removed after acceptance. Failed effects
prevent the source checkpoint from advancing; unknown append outcomes recover
through the same operation identity.

RoleCleanup sends the discovered exact assignment identity and revision. Its
removal effect identity binds source event, target owner and assignment episode.
A delayed removal cannot mutate a replacement episode. Complete source cuts,
late relationships and the remaining cleanup observations are still outstanding;
this slice does not claim to complete those protocols.
