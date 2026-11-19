# Per-user platform operator owner review

Status: **source implementation and focused tests authored; not compiled or
tested**, 2026-11-05. This is one aggregate slice, not a migration, integration,
provider qualification or production-readiness claim.

## Boundary and lifecycle

`PlatformOperator` is identified by platform and `UserId`; its stream is
`stream://bdgrz/platform-operators/{deterministic-owner-id}`. The owner holds
one current grant episode, one optional primary mark, one lineage and scalar
generation high-water mark, and only the selected historical operation outcome.
It does not retain ordinary operators, a roster of operations or seat children.
Portia still loads the full per-user stream; low churn and a safe history
rollover/restore policy have not been measured or qualified.

Each of the six concrete business-event JSON payloads is checked against an
8 KiB ceiling before emission and on replay. Human attribution is bounded in
UTF-8 and escaped form before serialization, and reason validation preserves the
existing trimmed nonempty 500-character rule without allocating a copy of
unbounded surrounding whitespace. Malformed UTF-16 is rejected. This is a
business-payload check, not proof of the final attributed persistence envelope;
the latter requires a separately enforced store-boundary limit.

The target atomically refuses grant revocation while marked. A grant episode
is the grant operation identity; a new grant after revocation is a new episode.
An exact source-verified reservation can mark the current episode at a new
generation. A target-owned fence prohibits a delayed mark for its generation,
including after subsequent unrelated operations. Exact source-verified release
can unmark only the same current reservation, episode, lineage and generation.
The first bootstrap target records grant and mark in one fact only if this user
has never actually held a grant. A preceding target-side fence may exist: a
subsequent independently qualified bootstrap reservation in the same lineage
must use a strictly higher generation. It cannot revive the fenced generation
or bootstrap-regrant a previously granted and revoked user.

The selected operation supports exact-intent retry and rejects changed intent;
no-op commands on a new identity do not claim an accepted outcome. A caller must
independently verify the committed seat request or release, exact actor
admission, freshness and original expected target position. An event ID alone
is not proof. The seat owner still must enforce at most two confirmed or
pending primaries and replacement first. Neither this owner nor its methods
proves or implements that cross-owner invariant, bootstrap authority, startup
or migration. Different primary lineages fail closed until a separate fenced
continuity protocol is specified and implemented.

## Event naming and review

The legacy roster event types and discriminators remain unchanged. Six new
per-user event types have distinct v1 discriminators:

| Event | Discriminator |
| --- | --- |
| `PlatformOperatorAccessGranted` | `bdgrz.platform.operator.access.granted` |
| `PlatformOperatorAccessRevoked` | `bdgrz.platform.operator.access.revoked` |
| `PlatformOperatorBootstrapGrantAndMarkAccepted` | `bdgrz.platform.operator.bootstrap-grant-and-mark.accepted` |
| `PlatformOperatorPrimaryMarkAccepted` | `bdgrz.platform.operator.primary-mark.accepted` |
| `PlatformOperatorPrimaryMarkFenced` | `bdgrz.platform.operator.primary-mark.fenced` |
| `PlatformOperatorPrimaryUnmarked` | `bdgrz.platform.operator.primary.unmarked` |

The first Jev [request](platform-operator-event-names-jev-request.json) and
[response](platform-operator-event-names-jev-response.json) returned
`jev-1.14.0`: grant 0.62, revoke 0.64, bootstrap 0.76, initial `PrimaryMarked`
0.41, unmark 0.79, and initial `DesignationFenced` 0.62. Those are advisory yes
probabilities, not correctness evidence. The low mark result prompted a
[follow-up request](platform-operator-event-names-followup-jev-request.json) and
[response](platform-operator-event-names-followup-jev-response.json):
`PrimaryMarkAccepted` 0.77, `PrimaryMarkRecorded` 0.75, bootstrap grant-and-mark
accepted 0.71 and `PrimaryMarkFenced` 0.78. The selected mark/fence names state
the target outcome and avoid implying the seat is confirmed. The
[grant-name follow-up](platform-operator-grant-names-jev-request.json) and
[response](platform-operator-grant-names-jev-response.json) returned 0.77 for
access granted and 0.71 for access revoked; synonyms were close. The existing
product language says grant/revoke operator access, so those names are retained
with distinct discriminators rather than silently repurposing legacy events.

## Tests and remaining work

Focused tests are authored for distinct per-user owners, exact retry and
changed intent, refusing revocation of a mark, target fencing and late marks,
stale unmark after redesignation, atomic bootstrap/replay, single-candidate
bootstrap after a target fence, no configuration regrant after revocation, a competing revocation winner, oversized attribution,
escaped payload expansion and replay. They were **not run**, and no compilation,
formatter, provider, host or CI result is claimed.

The coordinator must add all six new event types to generated JSON/event
catalog/serializer contracts and update their fixtures without reusing the
legacy discriminators. No handler, API, MCP route, admission adapter, seat owner,
worker or registration is included. The selected
[protocol](platform-operator-primary-protocol.md) still requires exact
participant/source validation, seat transitions, per-operation reconciliation,
conditional-append and lineage guarantees, initial credential/configuration
eligibility, safe bootstrap, bounded legacy migration, old-writer fencing and
durable cutover before the new owner can become authoritative.
