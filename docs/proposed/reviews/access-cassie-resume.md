# Access Cassie resumed preparation

Tracking: [#1176](https://github.com/bdgrz/compliance-core/issues/1176).

Status: prepared source, **not executed acceptance**. The user resumed processing
on 2026-11-04. The original isolated repository commits are `f6cdc122`,
`d0d29064`, `6d880189`, and `226d00d6`; do not integrate their unrelated ancestry.
The pinned-engine LIMIT and TEXT corrections remain included.

## Scope and source review

The seven repositories preserve the current Team/Role directories and permission
graph. They do not implement custom roles, scoped TeamRole identity, guest caps,
AccessGrant retirement, or the rest of the approved Platform access context.
The six directories store typed identity/content columns; their full projection
scope and requested identity predicates qualify each returned row. Permission
state is the sole serialized body. Its stored TenantId is checked both before
applying another fact and before returning an explanation or allowing a grant.

Independent review requested explicit stored-body qualification. Two new native
cases corrupt the serialized graph TenantId while keeping its target scope key,
path identities, and materialized grant. They assert an empty explanation and
permission denial, then refusal of a subsequent fact without replacing the graph,
materialized grant, or committed checkpoint. These cases are authored and
unexecuted; source review does not establish native acceptance.

## Feature-local fixture preparation

- `TechnologyInventoryScopedGrantTests` owns an isolated native lease and uses the
  real Cassie permission and role-permission repositories. Existing membership,
  NoTeams, restricted visibility, expiry, and revocation assertions remain.
- `ArtifactMetadataAccessTests` owns a native lease, configures it before
  AddCompliance, and keeps the registered ProjectorRunner catch-up path.
- `EvidenceRedactionCompositionTests` gives secondary providers the parent
  fixture's native connection while its lease remains alive. Its clock decorator
  still delegates every permission decision to the real implementation.
- The five controlled paged-read doubles use the access-specific invalid-cursor
  exception. Existing handler assertions remain.
- The excluded SplitHost E2E source names the native authorizer so the assembled
  solution can compile. This is not E2E qualification.

Fixture providers/scopes must be disposed before releasing their database leases.
Setup failure paths also release the leases. No shared helper or analyzer settings
were changed. `git diff --check` passed; no restore, build, test, or format command
was run in this preparation lane.

## Coordinator acceptance

The saved shared checklist still applies: application schema must include eight
access tables, aliases must share the native scoped concrete instances, and all
seven stable workload names must use native projection registration. Update
schema assertions and any shared test coverage paths centrally. Compile only
after these changes and this packet are assembled on the current accepted base.

Run `CassieAccessControlRepositoryTests`, `CassieAccessControlPagingTests`,
`CassiePermissionAuthorizerTests`, all six Cassie directory classes,
`RbacPagedReadContractTests`, `ArtifactMetadataAccessTests`,
`EvidenceRedactionCompositionTests`, `TechnologyInventoryScopedGrantTests`,
`CassieReadModelSchemaTests`, and production composition tests. Exercise all seven
registered projectors; direct repository Begin/Apply tests alone do not verify
shared wiring. Retain actual permission and projection behavior in fixtures.

Observed behavioral RED is still pending for this storage packet. Missing-type or
setup/compilation failures are not behavioral RED. The new stored-body cases
qualify existing defensive behavior and are expected to pass once assembled.
After focused/native execution, run the ordinary full backend unit gate, strict
Release build, whitespace verification, final-head review and CI before delivery.
