# Platform operator primary protocol

Status: **replacement-first and two-primary policies approved; provider unqualified**,
2026-11-05. The product owner approved replacement first: the last designated
primary cannot be revoked until another primary is designated and confirmed.
The user also approved at most two confirmed or pending platform primaries;
ordinary operators remain unlimited. The selected two-seat model below
specifies the intended owner protocol. It does not claim that the broker, SDK,
credential service, migration barrier, workers, or existing code provide the
required guarantees.

The current implementation remains `PlatformOperatorRoster` until the
cutover described below completes. The [adversarial review](platform-operator-primary-protocol-review.md)
retains the Jev request, response and probabilities. Jev is an advisory review
signal; the source and expected-position arguments below carry the design.

## Selected primary capacity

The selected model treats every designated primary as a protected seat. The
user approved at most **two confirmed or pending** primaries, even when many
ordinary operators exist. A third designation waits or is refused; ordinary
operator grants are not capped. A releasing seat remains occupied until its
exact target unmark is confirmed, so capacity is conservatively unavailable
during cleanup.

An unselected alternative would allow any number of independently designated primaries
while keeping at most two internal **protected keeper** seats. The keeper seats
are a subset, not a count or roster of all primaries. A protected keeper cannot
be revoked until another exact designated primary is promoted and confirmed as
keeper; a non-keeper primary may be revoked because at least one keeper remains.
Promoting an already designated primary would be an internal replacement step,
not a new user-facing designation cap. This alternative needs its own complete
target-owned designation/keeper marks, promotion-versus-revocation ordering,
bootstrap, cleanup and migration proof. In particular, a stale read of an
unprotected replacement is not confirmation. The user has not selected that
model, and this document does not claim it is already implemented or qualified.

The user selected the two-primary model, so the keeper-subset alternative is
not part of this implementation.

## Two-seat owners and invariants

| Owner | Identity and bounded current state | Atomic decision |
| --- | --- | --- |
| `PlatformOperator` | Platform and one `UserId`; current grant episode, optional current primary mark, and a scalar highest settled designation generation. | A marked primary cannot be revoked. Grant, mark, unmark and revocation of this user compete on this user's stream. |
| `PrimaryPlatformOperators` | Platform; at most two seats including reserved, confirmed or releasing seats, a bootstrap/cutover mode and the next designation sequence. | After initialization at least one confirmed seat remains; a seat is released only when a different confirmed primary is still protected. |
| One operation or reaction | Exact initiating event, operation identity, target and generation. | Record immutable intent and durable retry/fence progress for this one operation; unknown is pending. |

Neither owner stores a roster of ordinary operators. Active grants and previous
facts are discovered from their own sources or read models. The two seats bound
current seat state, **not lifetime event history**: Portia replays each entire
stream. Measure expected platform-primary rotations and per-user grant churn;
history rollover, retention and restore need a separate qualified protocol
before claiming a physical stream-size bound.

The seat owner issues each reservation a strictly increasing, non-reused
designation generation in one platform lineage. A generation includes that
lineage and sequence; a new lineage is valid only through an explicitly fenced,
durable continuity transition, never by silently resetting a counter. The
target stores a scalar high-water mark sufficient to reject an older mark or
fence; it retains only the current mark and any selected operation outcome, not
a collection of historical generations. Exact historical retry is recovered
by selecting its operation while replaying the target stream, or from its own
durable operation record. Exhaustion or uncertain lineage is unavailable.

Every human command has a client operation ID and immutable authored intent,
including its target, reason and expected revision where applicable. A bounded
per-operation owner may claim that ID; it must distinguish exact retry from
changed intent and cannot infer success from an empty stream. An accepted
grant/revocation records the actor, subject, time and reason as required by
[M0-D25](../decisions/m0-d25-client-tenancy.md). System callbacks carry the
exact committed initiating fact, not caller-supplied authorization. Event names
and discriminators must be reviewed against the final payloads; in particular,
the existing roster's persisted `PlatformOperatorGranted` and
`PlatformOperatorRevoked` discriminators must not be silently reused for new
contracts with different owners or payloads.

## Grant, designation and release

1. A currently authorized operator requests a grant on the target user. The
   target records a new grant episode or returns the exact prior operation
   outcome. It does not become primary merely because it is active. Every
   decision uses server-produced admission bound to the actor and exact intent.
2. To designate an active operator, reserve one free seat at the seat owner's
   exact version. The reservation contains the operation, target user,
   designation generation and intended grant episode. Duplicate users cannot
   occupy two seats. A seat in cleanup is not free. The reservation is not yet
   a confirmed primary and cannot justify release of the other seat.
3. A durable per-reservation process independently reads the exact committed
   reservation, then asks the exact target to mark that generation and grant
   episode. At the target's qualified current head, mark and revoke use
   conditional append at the same observed position: the winner is the only
   valid decision. A revoked or mismatched grant cannot be marked; a marked
   grant cannot be revoked. A callback ID, a projection, and a successful
   advisory read are not participant outcomes.
4. Confirm only after independently reading the target's exact committed mark,
   owner address, user, generation, grant episode and reservation identity,
   and qualifying that the same mark is current and protected. The seat must
   still hold that exact pending reservation when confirmation is appended.
   Recheck and append against the seat owner's qualified version. An old
   accepted mark cannot confirm a later reservation.
5. A release names the old confirmed seat and a different confirmed replacement.
   The seat owner decides against its current exact version; two releases from
   the same version compete, and the loser reloads and cannot release the last
   confirmed seat. A reserved, refused, or historically confirmed but released
   seat is not a replacement. A released seat enters `releasing` and remains
   occupied until cleanup is proved; the replacement remains protected.
6. The target unmarks only after independently reading the exact committed seat
   release, and only if its current mark still equals that release's user,
   grant episode and generation. The seat clears the releasing slot only after
   independently verifying that target-owned unmark outcome. A delayed unmark
   of generation `g1` cannot clear `g2`. A new designation of the same user
   waits for `g1` cleanup; no silent rebind or no-event success is assumed.
   Revocation may then compete on that target's exact current stream position.

The above steps describe facts and required bindings, not a mandate to put
cross-owner child lists in either aggregate. All exact-source comparisons need
qualified source incarnation, retained history and commit ordering. Every
target append uses the captured expected position and cannot silently rebase.

## Refusal, uncertainty and recovery

A target refusal or cancellation is terminal only after an exact target-owned
fence or refusal fact for that generation wins the same original target
position as the attempted mark. The target's high-water mark prevents later
execution of that generation. If an unrelated target append already won that
position, first qualify the actual winner and retained source, then record a
fence for the old generation against the newly observed head; the old mark is
never rebased. A conflict or unknown acknowledgment is not a refusal. If the
mark won, recover its exact fact and confirm it before any requested release;
do not free the reservation based on timeout. A pending initial primary cannot
be released without a confirmed replacement.

Each source signal is durably admitted to one bounded per-operation retry
record before its shared input checkpoint advances. Its identity is derived
from the exact source fact and generation. A sweep can rediscover unfinished
records independently; one unavailable participant leaves its own operation
pending rather than blocking unrelated operators or the shared checkpoint.
An indefinite failure is visible for intervention, not automatically called a
refusal. Retry/disclosure requires current operator authorization. A previously
admitted exact uninterrupted append may finish at its original target position
after actor revocation; an unknown or fenced attempt requires fresh admission
before any new execution. A normal retry never changes its authored intent.

The current process-record store can retain a keyed intent, but the available
source-session and aggregate-executor APIs do not yet bind an original target
head to a durable incarnation or provide a qualified same-position append or
fence. The coordinator remains unimplemented until those guarantees and
ambiguous-outcome recovery exist. In particular, do not map an unknown or
conflicting participant to `Superseded`: the existing process driver can treat
that outcome as terminal outside cleanup stages.

## First bootstrap

Bootstrap is allowed only for a qualified never-initialized platform: a
trusted bootstrap system actor and an exact immutable configuration generation
must be validated, and the legacy owner must be proven uninitialized rather
than merely absent from a quiet read. Multiple starters compete for one
seat-owner initialization reservation; the committed winner fixes the first
configured user, operation, generation and source configuration generation.
The selected target atomically records its initial grant episode and matching
primary mark. The platform is ready only after the seat owner independently
confirms that exact target acceptance.

Preserve the existing behavior that all originally configured bootstrap users
are granted, rather than silently choosing only one. Once the first primary is
confirmed, import each remaining user from the **same immutable configuration
generation**, one independently owned user operation at a time. A durable scalar
cursor advances only after each exact target outcome is verified. The source
must enumerate that generation completely without materializing an unbounded
roster or requiring an in-memory sort. Bootstrap completes only when the
qualified enumeration reaches its end and the completion marker is committed.
Until then, changed or unavailable configuration does not add a new seed.
After completion, configuration is never a regrant authority. If a first target
is unknown, settle its exact append or fence before selecting a different
candidate. An empty configuration leaves the platform uninitialized and
unavailable. Eligibility and deterministic first-candidate selection must be
specified by the trusted configuration adapter; until its immutable generation,
complete enumeration and identity validation are qualified, bootstrap remains
unavailable. The current configuration adapter has not established these
properties.

## Legacy migration and authority switch

The existing roster stores one immutable `PlatformOperatorAuthorityContent`
reference containing active `(UserId, AssignmentId)` pairs and a revision. A
migration cannot rely on a projection, configuration or a scan racing live
writes. Select the **freeze-and-import** path:

1. Establish a provider-enforced write barrier that prevents every old roster
   writer and old bootstrap instance from committing, including in-flight and
   stale deployment processes. Drain or settle their exact unknown outcomes.
   A deployment flag, read-only advisory lock or quiet read is not a barrier.
2. Obtain the current, lineage-qualified roster head and exact committed
   immutable authority reference. Verify the referenced bytes, schema, digest,
   revision and active count. The current source uses a serialized array; any
   migration reader must validate and enumerate it incrementally, with bounded
   memory and no unbounded sort, or migration remains unavailable. Do not copy
   it into a new aggregate or claim an in-memory implementation is bounded.
3. Import each active assignment into its per-user owner, preserving the exact
   legacy assignment identity and source provenance. The migration operation
   is deterministic per cut and user. After a qualified exact target outcome,
   advance one durable scalar cursor; a crash rechecks that outcome before
   proceeding. Duplicate or malformed source entries, missing content, unknown
   target commits and conflicting target history stop the cutover. Historical
   grants and revoked users are not fabricated as current grants. Their legacy
   history remains available for audit and authorized old-operation retries.
4. Choose one imported active user as initial primary from that immutable cut,
   reserve and mark it under the same exact target/seat protocol, then confirm
   it. Complete the source enumeration and verify every item outcome before a
   durable single-authority cutover marker enables per-user admission. The
   existing roster remains authoritative and writes stay frozen until then.
5. After cutover, route reads, writes and authorization only to the new owners;
   permanently fence legacy grant/revoke and old bootstrap routes. Configuration
   cannot regrant users absent from the imported cut. A restart resumes the
   recorded phase and exact cursor; it cannot restart seeding. A restored store
   must preserve the barrier, cutover marker and non-reused source lineages or
   enter unavailable recovery mode.

No implicit rollback to the old roster is safe after new writes: it would lose
new grants and revocations. Use a separately qualified freeze and reverse
reconciliation, or a forward repair, before changing authority again. The
provider-enforced old-writer barrier, immutable complete snapshot enumeration,
source/current-head qualifications and compatible deployment procedure do not
yet exist as qualified implementations. Migration and bootstrap must stay
disabled until they do.

## Implementation and qualification boundaries

The approved replacement-first rule, two-primary capacity and decision ordering
above establish the aggregate boundaries. Before claiming the end-to-end feature
usable, implement and qualify the operation identity and
fence, target and seat owners, independent durable reconciliation, source
admission, bootstrap source adapter, migration barrier/import, and cutover.
Exercise competing releases, mark/revoke races, ambiguous commits, delayed
mark/unmark, canceled reservations, stale configuration, concurrent legacy
writers, restart, source replacement and restore. Event names, exact wire
schemas and bootstrap candidate eligibility/order remain to be finalized from
their implementation contracts; do not mistake those open details for provider
qualification. No build or test was run for this design.
