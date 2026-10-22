# Event and KV persistence limits

The CORE-01 correction under [#1022](https://github.com/bdgrz/compliance-core/issues/1022)
uses the pinned Portia 0.8.1 serializer and Fitz.Core 2.0.0 operation framing.
[The audit baseline](../product/audits/2026-10-22/payload.json) records the exact
package hashes, source commits and SDK evidence.

`DomainEventPayload` measures a concrete event with generated JSON metadata even
when a factory returns `DomainEvent`. The fourteen affected owner guards and the
additional identity-provider/sign-off guards use a 48 KiB business-payload
margin. This is an early storage check, not a lifetime business limit or proof
that the final attributed envelope fits. No reflection serialization fallback is
used by this helper. Unit coverage reconciles concrete generated metadata for
every domain event in Common and Core.

The application decorates `IEventStore` after Portia composition. Aggregate saves
have already attached their final actor/execution/causation metadata when they
reach this boundary. `PersistenceEventStore` serializes every event in the batch
with the same registered durable serializer and checks all of them before
calling the inner store. It round-trips each checked envelope into a detached,
privately owned event and verifies exact canonical-byte parity. Only those
snapshots reach the inner append, so caller-owned nested collections cannot
change the actual payload while opening the session yields. A parity or
serialization failure opens no append session. An oversized later event also
rejects the whole batch before a session opens. The inner store retains batch validation,
OCC, synchronous commit and rollback behavior. Reads and event notifications
forward to the original store. Reader/writer/notifier aliases resolve the same
decorator; original service lifetimes and disposal stay owned by DI.

For Portia's stream appends, let `E` be the complete serialized envelope and `R`
the UTF-8 byte count of the actual stream route. The SDK operation is:

```text
8 session + 8 expected offset + 4 body length + E
+ 1 metadata present + 4 route length + R + 1 absent discriminator
= E + R + 26 <= 65,535
```

`FitzLimits.MaxFrameSize` includes transport headers and correlation; it is not
an event, envelope or KV-value budget. A correlated extended stream request can
reach 65,551 bytes while its operation payload remains at most 65,535 bytes.
Unicode and JSON escaping count as encoded bytes, not string lengths.

`PersistenceKvClient` decorates the shared KV client, including projection and
checkpoint users. Each transaction checks the exact route and operation fields
before forwarding. Put/insert require `value + UTF8(route) + key + 20 <= 65,535`.
Get/delete use `route + key + 16`; range deletion includes both key lengths;
scan includes each present bound/limit/exclusive flag. Begin and finalization
include their own overhead. Reads, durability, cancellation, rollback, disposal,
exclusive-scan support and subscriptions retain the inner client's behavior.

Known event-frame rejection becomes a nontransient `Validation` result through
the shared request bus after normal dispatch authorization. Both result-bearing
and no-result operations use this path, so transports retain their existing
error mapping. KV projection failures, serializer failures, cancellation and
OCC remain exceptions with their original meaning. An uncertain append/commit
outcome is never reclassified as a known pre-append rejection.

The focused regressions first demonstrated that an oversized concrete incident,
an oversized later event and oversized KV writes were accepted. Boundary tests
use real attributed envelopes, Unicode route/key bytes and the pinned SDK codec,
including exactly 65,535 operation bytes and one byte over. Other tests cover
large save attribution through production composition, whole-batch serializer
failure, payload mutation while opening a session, notifications, aliases and
validation/error propagation. Immutable-head review reproduced the mutation
gap and made its correction a durable regression.

This correction changes neither retained aggregate state nor physical replay
length. Existing histories, broad snapshot payloads, per-owner lifetime caps and
immutable-content/manifest corrections remain assigned to their structural
delivery families. Removing arrays later will not shorten existing replay; no
snapshot, truncation, new lifetime cap or environment reset is implied here.

Jev evidence includes the initial design and immutable-head preflight plus a
bounded refinement question in `docs/product/audits/2026-10-22/`. The first
preflight requested revision (0.58); its follow-up assigned no demonstrated
blocking gap (0.63), but the mutation regression disproved that assessment.
The adopted detached-snapshot correction follows the executable result. Jev
assigned 1.00 to that conditional correction; probabilities do not replace the
regression, full suite, adversarial review or final-head CI.
