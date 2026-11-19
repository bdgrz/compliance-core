# Primary platform operator seats: aggregate review

## Boundary and invariant

`PrimaryPlatformOperators` owns exactly two current seat slots. It protects the
rule that a confirmed primary can be released only while a different confirmed
primary remains. Pending reservations and releases awaiting target cleanup also
occupy a slot. Ordinary operator grants belong to per-user `PlatformOperator`
owners, not this aggregate. Replay retains the two slots, scalar lineage and
generation, and at most the one explicitly selected operation; it does not retain
an operation history or enumerate operators.

An initial seat is reserved and confirmed only by the exact target bootstrap
grant-and-mark outcome. A target fence refuses the matching pending reservation;
a released seat remains occupied until its exact target unmark is recorded.
Exact selected-operation retries are distinguished from changed intent. A later
seat release must observe another confirmed seat at its expected owner revision.
After a fence on an otherwise never-granted bootstrap target, that same user may
be retried with a later generation in the same lineage. A prior actual grant,
even after revocation, continues to disqualify bootstrap regrant.

`BootstrapPlanId` is only the scalar identity of a separately qualified immutable
configuration generation. It is not a manifest, list, catalog-sized record, or an
aggregate holding operators. This owner never enumerates configured users.
Confirming the first primary does not establish completion of bootstrap.

## Facts and naming review

| Fact | Persisted discriminator (version 1) |
| --- | --- |
| `PrimaryPlatformOperatorSeatReserved` | `bdgrz.platform.primary-operator.seat.reserved` |
| `PrimaryPlatformOperatorAppointed` | `bdgrz.platform.primary-operator.appointed` |
| `PrimaryPlatformOperatorSeatRefused` | `bdgrz.platform.primary-operator.seat.refused` |
| `PrimaryPlatformOperatorSeatReleased` | `bdgrz.platform.primary-operator.seat.released` |
| `PrimaryPlatformOperatorSeatVacated` | `bdgrz.platform.primary-operator.seat.vacated` |

The [initial Jev review](primary-platform-operator-event-names-jev-response.json)
favored reservation (0.87), refusal (0.74), and release (0.78); alternatives for
confirmation and cleanup needed additional review. The
[final review](primary-platform-operator-event-names-final-jev-response.json)
favored `Appointed` (0.80) and `SeatVacated` (0.76). These are advisory naming
signals, not evidence of protocol or runtime correctness. The new facts have
distinct identities from the existing operator roster grant and revoke facts.

## Verification and remaining work

Focused tests were authored for initial confirmation, target identity and
metadata, the two-seat bound, competing releases, exact fence and unmark outcomes,
delayed cleanup, and retrying the sole bootstrap candidate after a fence. They
have not been compiled or run, as requested. New and replayed business facts are
subject to the 8 KiB serialized business-payload limit; the attributed-envelope
limit is a separate persistence boundary.

Attached target facts are not authority: callers must independently verify exact
committed participant events, their source lineage and retained coverage, required
current target protection, actor admission, and qualified conditional writes.
Capture and enforce the expected source and target heads without silently
rebasing. Configuration qualification and enumeration, durable operation claims,
worker recovery, bootstrap completion proof, migration barrier and cutover, and
runtime activation remain separate unfinished choreography. No source provider or
worker is made qualified by this aggregate or its registration. Historical
selected-operation outcomes are retry receipts, not proof of current entitlement.

The durable per-attempt coordinator remains unimplemented. The repository's
`IProcessRecordStore` supplies create-if-absent and versioned record replacement,
but does not bind a process record to a target stream incarnation or make a target
append atomic with it. The current qualified read-session seam supplies only a
point-in-time exclusive head; it has no durable source-incarnation identity or
qualified conditional append/fence. A provider must bind the exact initiating
fact and target incarnation, lineage, physical head and aggregate revision to a
durable attempt; enforce the original-position append or fence; and independently
recover the exact winner after an ambiguous commit. Retries reuse that attempt,
changed intent conflicts, and unknown stays pending. An unrelated winning append
must be qualified before a separately authorized later fence; the old mark is
never rebased. These properties are not supplied merely by storing bytes.

The generic `ProcessDriver` treats `ReceiverOutcome.Superseded` as completion.
No participant receiver may map an unknown append, a concurrency conflict, or
missing source proof to that outcome; it must remain retryable until an exact
outcome or qualified fence is established. No worker or granting provider is
registered for this unfinished choreography.
