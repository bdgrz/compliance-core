# Tenant slug, team and role operations

[#1022](https://github.com/bdgrz/compliance-core/issues/1022) slice S15 closes
CHOREO-08, ACCESS-09, ACCESS-14, ACCESS-15 and ACCESS-16 and the role and team
part of CHOREO-06. The member-retirement part of CHOREO-06 shipped earlier.

## Tenant and TenantSlug

`ChangeTenantSlug`, the tenant answers (`ConfirmTenantSlug`, `RejectTenantSlug`,
`RequestTenantSlugSurrender`, `ConfirmTenantSlugSurrender`,
`RejectTenantSlugSurrender`) and the slug owner commands (`RegisterTenantSlug`,
`SurrenderTenantSlug`) carry a `CommandId`. Each is a root native operation on
its first owner: the accepted fact carries `OperationProvenance` and a frozen
Unit first response. An exact retry replays without appending and a changed
binding conflicts.

The public change is `PUT /api/v1/tenants/{tenant_id}/slug-changes/{command_id}`
with a required signed `expected_revision`: the tenant revision from
`GET /api/v1/tenants/{tenant_id}` (`revision`). Only system callers may send a
negative value. The slug owner is read as a possibly stale preflight after the
retry is resolved, never before.

The slug owner appends at most one fact per logical operation. A second claim by
the tenant that already holds the slug and a surrender of an already surrendered
slug append nothing. A refusal is one fact per claim, so duplicate or repeated
claims cannot grow the stream (ACCESS-14).

`TenantSlugReactor` and `TenantRegistrationReactor` derive every command id from
the source fact (`TenantSlugEffects`), so a replayed source fact repeats the same
logical operation. The tenant treats an answer for a slug it no longer waits for
as a named terminal no-op (`Tenant.StaleSlugOutcome`), so a replayed or late
receiver can never fail and block the global checkpoint (CHOREO-08). The tenant
no longer keeps previous slugs to swallow late acknowledgements (ACCESS-16).

`TenantOwner`, `RegisterTenantOwner` and `TenantOwnerRegistered` are removed:
`TenantRegistered` already records the owner and no rule lived in the duplicate
(ACCESS-15).

## Team and Role

`DefineTeam` and `DefineRole` are accepted under the team or role id as the
immutable creation identity. The same id with a different name conflicts
(ACCESS-09). `DeleteTeam`, `DeleteRole` and `RenameRole` carry a `CommandId`;
deleting an already deleted owner and renaming to the current name append
nothing. Routes: `PUT .../teams/{team_id}`, `PUT .../teams/{team_id}/deletions/{command_id}`,
`PUT .../roles/{role_id}` and `PUT .../roles/{role_id}/deletions/{command_id}`.

The built-in bootstrap treats a conflicting built-in definition as settled, so a
catalog name migrated by `RenameRole` cannot block the bootstrap checkpoint.

## Cleanup discovery

`RoleCleanupReactor` and `TeamCleanupReactor` no longer page a directory. They
fold the authoritative assignment facts of the tenant area from its start
(`RbacRelationshipSource`) and send one removal per live relationship carrying the
exact assignment episode, revision and generation. A removal that loses to a
concurrent removal or a newer episode is settled only after the owner is read
again and no longer holds the discovered episode; a newer episode's authority is
never removed. Assignments made after a role or team deletion are not cleaned up
here.
