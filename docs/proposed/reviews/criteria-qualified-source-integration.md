# Qualified source integration for criteria

Status: **under reassessment**. The user rejected a separate authority namespace,
independent recovery record, synchronous-only profile and coordinated rollback
requirement. This document records earlier analysis, not a prerequisite or
selected implementation. Reassess each remaining requirement against concrete
feature invariants and the existing Portia/Fitz contracts.

## Evidence the feature needs

An exact-resource read needs a broker observation bound to its resource, route,
source incarnation, committed head and retained boundary. Every page and the
final qualification must belong to that observation. The consumer checks
contiguous offsets and fails pending on an unknown head, a gap, expiration,
lineage change, lost authority or unresolved commit. A numeric offset sampled
after a read is insufficient: restore or replacement can make the same offset
refer to a different history.

An absent Fitz resource head currently falls back to a scan; an empty result
cannot distinguish a new resource from missing or expired history. A genesis
marker is safe only in an explicitly provisioned authority namespace whose
initial emptiness and storage integrity are qualified, with atomic creation and
separate unknown/imported provenance for snapshot replay. Existing resources
must remain unknown until a sound migration establishes their history.

The edition and its entries share an area; publication and parent checks also
need a qualified area cut. Label claims live in another area. Label choreography
therefore needs a common qualified realm order or a durable writer attestation
that binds both exact committed facts and their lineages. Different areas'
offsets, event IDs and unqualified references do not prove cross-area order.
The runtime discovery projection additionally needs a contiguous qualified realm
checkpoint, the exact publication marker and independently verified participant
outcomes. It must accumulate that evidence incrementally, not load a roster to
decide a write or serve a page.

## Cross-layer work

1. Qualify Fitz writer recovery, resource genesis and legacy metadata,
   retention, incarnation changes, routing, durability, and restore/reset
   behavior. A process-lifetime token can invalidate in-flight observations on
   restart or replay, but cannot by itself protect durable decisions after an
   offline rollback. The proposed independent anti-rollback authority and
   coordinated recovery profile were not selected.
2. Negotiate explicit broker operations and page evidence; keep unsupported or
   unqualified sources unavailable. Carry the evidence without defaults through
   the Fitz SDK and a separate Portia qualified-reader contract.
3. Replace the provisional `ICriteriaSourceCutCoverage` and fresh-head calls
   with contracts bound to the actual read observation; implement
   `ICriteriaParticipantCausalOrder` using qualified realm order or equivalent
   durable attestations. Bind projection checkpoints to the same guarantees.
4. Qualify the real credential, policy and permission owners used by
   `IAccessAdmissionAuthority`; wire the authoring and publication workers,
   seed writer, API and MCP only when their required evidence is available.

The isolated Fitz worktree contains provisional stale-head recovery,
restore/actor fencing, bounded-memory fallback scanning, and a private family
incarnation that rotates before replay. It also reserves an unavailable-only
Wire 3 operation whose capability is not advertised; no successful observation
payload is defined. It exposes no qualified capability, and none of those
changes has been compiled or tested in this work chunk.
An isolated Portia v0.10.0 worktree also contains provisional opt-in sessions
for exact-resource reads and bounded, unfiltered area or realm enumeration.
The latter describes source continuity, not verification of participant outcomes.
Neither has a granting adapter, and neither has been compiled or tested.
An isolated Fitz .NET 4.0.0 worktree contains a matching unsupported-only client
foundation. It neither advertises the capability nor sends a request in production;
future negotiation and dispatch must be bound to the same connection.
