# Scoped team permission authority — 2026-11-05

Status: **partial source checkpoint** for PA-01/02/03/17/18. The strict build,
whole-solution whitespace, six Node cases and proposal/runtime-fixture validators
pass. The 296-case scoped-access/actor/bootstrap/coverage selection passes; the
full domain gate fails. See [validation evidence](scoped-team-access-validation.json).

## Identity and invariant

- Role owns its name and terminal lifecycle. RolePermission independently owns
  one known permission assignment for one role. Built-in definitions/permissions
  are locally protected; custom roles are usable.
- TeamRole owns one team/role/**scope** assignment and its assignment episode.
  Organization, program, engagement and typed shared-resource owners are distinct.
  Org Admin is organization-only; Advisor/Attest cannot be assigned through teams.
- Member owns the immutable allowed-program set of its current guest episode,
  with finite UTC expiry. A new membership episode cannot inherit an old team's
  assignment. A Person/member uniqueness owner is not part of this packet.
- Keep these owners separate: no tenant permission roster or cross-owner atomic
  invariant is introduced. Permission evaluation reads current owners; it is not
  a global transaction that freezes independent streams for a command's lifetime.

## Implemented source

Recovered only the custom/scoped-role and guest-cap commits from the preserved
Access branch, not its broad ancestry or another storage migration. The new
TeamRolePermissionAuthorizer backs all four production permission interfaces and
IMemberAccessReader. Cassie's permission graph remains a projection, not the
positive authorization decision. Each selected path requires current membership,
matching membership/assignment episodes, an active tenant/team/role and its current
permission. A final selected-path check handles removals committed during the
initial permission read; membership is checked again afterwards.

Program and typed shared-resource assignments do not confer standing organization
permission. Existing application/system-instance scope callers map to the exact
shared-resource kind and ID. Guests receive the intersection of their membership
programs and team-derived authority, never organization-wide standing permission.

The four production aliases have no direct-grant fallback. GrantAccess and legacy
guest provisioning are unregistered; grant HTTP/MCP surfaces are removed. Historical
contracts, old implementation classes and revocation/cleanup remain. The guest
invitation reaction carries its accepted program manifest into RegisterMember and
assigns the Viewer team, rather than issuing direct grants.

GetTeamMemberAccessHandler exposes scoped paths, keeps EffectivePermissions
organization-only, and returns no active direct-grant paths. Role rename is exposed
through typed HTTP/MCP. Scoped role removal uses **PUT** with JSON at the existing
`.../roles/{role_id}/removals/{command_id}` path: the complex Scope cannot use
Portia's default DELETE/query binder. Startup previously failed with “This request
shape requires OnBind”; the later full run no longer reports that failure. The
WebIntegration transport fixture is updated but was not executed by the unit gate.

Shared integration repairs add missing Finding command JSON roots, refresh the
runtime inventory through its existing explicit exporter, and correct stale
Fitz-to-Cassie source links. These are not new Finding business implementation.

## Executed evidence

- The first new evaluator selection observed three intended permission failures
  before implementation; two native setup failures in the same selection are not
  behavioral RED. Configuration/compilation harness failures are recorded separately.
- Guest-cap propagation and two exact resource-adapter checks failed before repair.
  A null-permission regression also failed before its explicit refusal guard.
- Five revocation-during-read cases failed before the final owner recheck. The
  full 68-case selection then passed; direct-grant non-authority and scoped-query
  cases extended the narrow selection to 71 passing cases.
- The expanded source selection passes **296/296**. It includes bootstrap recovery,
  source episode/expiry/refusal/replay checks, protected built-ins, custom roles,
  scoped authorizers, existing actor walls and the reflected runtime inventory.
- Old fixtures now supply explicit guest programs. Fixed-role tests follow the
  locked custom-role/built-in distinction; unauthorized actor tests still deny
  before replay. The cleanup stress case retains **205 historical permission
  relationships** even though new unknown permissions are refused.
- First full gate: 7,992 passed / 1,231 failed. After repairs: **8,141 passed /
  1,082 failed**, zero skipped, with all 6,713 compiler/fixture inputs unchanged
  during that run. BrokerIntegration/WebIntegration were excluded as required.
- The second full run has 540 transactional-DDL failures, 481 connection-exhaustion
  failures and 61 other assembly failures. Two expired-guest fixture corrections
  followed it; all **21** Support withdrawal handler cases then passed. This is
  a focused supplement, not a final-head full-suite pass.

## Bounded Jev review

[Request](scoped-team-access-jev-request.json) and
[response](scoped-team-access-jev-response.json) retain one `jev-1.14.0` call,
7,043 input / 126 output tokens. No key is stored. Adverse probabilities:
organization-scope leakage .11; guest-cap leakage .08; missed removal during the
first permission read .22; legacy DI fallback .29; explanation flattening .11;
invitation cap loss .11.

The .22/.29 signals are not model clearance. Source inspection and executable
checks establish the narrow cases: all four production interfaces resolve to the
same source evaluator, old direct grants do not authorize, and five selected-owner
revocations during the initial permission read deny. The three hashed source files
in the request remained unchanged through the final compiled candidate. This is
not an exhaustive security review or final PR-head review.

## Remaining acceptance and next source

- Resolve native startup [#1188](https://github.com/bdgrz/compliance-core/issues/1188)
  and the broader assembly failures in
  [#1189](https://github.com/bdgrz/compliance-core/issues/1189), then qualify the
  scope-bearing native repositories and full backend gate. Do not mask failures
  with application retry machinery, permissive fakes or skipped tests.
- Finish command-side target/reference admission and full scoped public transport
  acceptance; raw guest program caps are not yet fields of the membership
  detail/list projection. Effective access explanations do include bounded scopes.
- Implement PA-27 primary seats/member marks/bootstrap before retiring the old
  ordinary-admin continuity guard. PA-19 deleted-team role housekeeping and PA-21
  rejected-first-slug recovery remain separate work.
- Existing SQL data needs schema/rebuild qualification for scope-bearing rows;
  guest history without a program cap is rejected. Backward compatibility was
  waived, but no data conversion or deployment is claimed.

No context is marked done. No PR, hosted acceptance, merge or release is claimed.
