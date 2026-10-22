# Platform-operator operations

[#1036](https://github.com/bdgrz/compliance-core/issues/1036) corrects the
PlatformOperatorRoster portion of ACCESS-08 under
[#1022](https://github.com/bdgrz/compliance-core/issues/1022).
The roster remains one atomic authority because revocation must retain at least
one current operator. Its retained map contains active assignment slots;
selected native facts resolve individual operations without a lifetime receipt
map or receipt-only domain event.

## Required intent

- `PUT /api/v1/platform/operator-grants/{command_id}` requires `user_id`,
  `reason` and original `expected_revision` in JSON.
- `PUT /api/v1/platform/operator-revocations/{command_id}` also requires the
  exact target `assignment_id`.
- `GET /api/v1/platform/operators` returns the roster `revision` and `operators`
  rows containing `user_id` and `assignment_id`.

The same identities are required ordinary MCP arguments. Portia 0.9.0 binds
command identity to logical request metadata while attempts retain independent
execution identity. Conflicting route/body values and missing, malformed or
empty identities fail validation. No server-generated substitute or new
transport listener is introduced.

Authorization runs before selected-fact hydration. The roster also checks the
actor's current operator status before resolving accepted intent. A revoked
actor cannot use an old command identity to bypass that authority. Original
actor provenance is credential-free.

## Native facts and recovery

Grant identity is its assignment episode. Revocation closes the exact selected
assignment, preserving last-operator availability atomically. Native facts keep
command identity, original revision, actor, subject, canonical reason and target
episode. Exact old retries return their original success without appending,
including after revocation or reassignment. Changed actor, kind, subject,
original revision, reason or episode conflicts. New operations enforce the
current revision and active slot; an already active subject cannot acquire an
unrecorded successful grant identity.

Canonical reason trims surrounding whitespace. Server timestamps and current
actor display are not client-authored retry intent; accepted facts retain their
original values. An accepted operation is resolved before checking the target
user directory, so later projection lag cannot defeat an exact retry.

Initial seeding uses the existing fixed roster creation identity as a required
typed value. Initial assignment identities derive deterministically from that
creation and each user. Explicit creation retries compare the original canonical
user set and actor. Startup retains the seed-once configuration policy: an
already initialized roster is not reconfigured on restart. Competing initial
hosts recover the winning append by resending the same typed creation command.
A new configuration cannot replace an accepted concurrent creation.

Tests cover competing revocations, concurrent startup, and unknown outcomes
after seed, grant and revoke commits. Revocation retry after a later grant leaves
the newer assignment intact. Failed or unknown appends never justify releasing
an accepted authority claim.

The roster retains active assignments and at most one selected native fact.
Physical hydration still reads its complete history and Portia retains replay
event identities. This does not make physical replay independent of history.

This is a direct breaking pre-release contract/state cutover. Existing integration
consumers are updated to supply identities and read exact assignments. No
compatibility path, deployment or environment reset is included. ACCESS-08 still
has the Member and TeamMember owner/continuation slices outstanding.
