# Team-role operations

[#1034](https://github.com/bdgrz/compliance-core/issues/1034) applies the
[selected native fact approach](role-permission-operations.md) to the TeamRole
portion of ACCESS-08. The canonical `(TenantId, TeamId, RoleId)` owner remains
atomic for one active role assignment. TeamMember, Member and
PlatformOperatorRoster remain separate slices; the complete audit stays open
under [#1022](https://github.com/bdgrz/compliance-core/issues/1022).

Assignment requires `CommandId` and the original `ExpectedRevision`. Removal
also requires the exact `AssignmentId`, which is the assignment's command
identity. The routes are:

- `PUT /api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}/assignments/{command_id}`
- `DELETE /api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}/removals/{command_id}`

PUT binds `expected_revision` from JSON; DELETE binds `expected_revision` and
`assignment_id` from the query. The JSON body can omit route values, and
conflicting duplicates are rejected. MCP takes the same required identities as
ordinary typed arguments. Portia 0.9.0 carries typed command identity as logical
request metadata and keeps execution attempts independent. No server-generated
client identity or new queue/remote listener is introduced.

Authorization precedes selected-fact hydration. Ordinary tenant-management
permission, the fixed built-in role catalog, membership eligibility and the
permanent client-management independence wall retain their existing authority.
The Administrators team's Org Admin role link cannot be removed. Neither an
exact retry nor possession of an old command identity bypasses current
authorization.

Native assigned/removed facts retain command identity, original revision and
credential-free original actor; removal retains the exact assignment episode.
Exact retries compare selected accepted facts before checking current state.
They return their original success without appending, including after removal
or reassignment. Changed actor, kind, original revision or removal episode
conflicts. New operations enforce the current revision and active episode;
a distinct assignment while already active conflicts without creating a
receipt-only fact.

The domain owner retains current episode state and at most one selected fact,
with no lifetime receipt dictionary or operation cap. Physical replay still
reads complete history, and Portia retains replay event identities. Domain
retained state and physical replay cost are separate properties.

Both team-role and role-team directories expose the exact assignment identity
and physical revision. RoleCleanup binds its effect to the source event, target
owner and discovered episode, and rejects foreign or incomplete rows. A delayed
removal cannot target newer authority. Complete source cuts and late relationship
discovery remain in the cleanup delivery family.

Bootstrap derives initial link command identities from source event, target
owner and lifecycle, retaining expected revision zero. Tests interrupt after
each of its four link commits, replay the same source, and retain intentional
removal of mutable links. The three critical administrator permission grants
still precede the assignment that triggers tenant activation. The 36-grant
permission recovery checks continue to apply.

The unused RbacGraph implementation has been removed; its sole test consumer
now exercises the production PermissionProjectionState with the actual
membership episode. This avoids retaining a second grant graph that did not
fold removal facts.

This is a direct pre-release contract/state cutover. No compatibility path,
deployment or environment reset is part of the implementation.
