# Qualified Fitz source: minimal implementation and deployment plan

Status: **not selected**. The user rejected the proposed separate authority
namespace, independent recovery record, synchronous-only profile and
coordinated rollback-recovery requirement on 2026-11-05. Retained as a design
record, this is not an implementation target or a prerequisite for the feature.
It does not assert
that existing broker, SDK, Portia, cloud storage or deployment code meets the
contract. The [integration note](criteria-qualified-source-integration.md) and
[Jev review](criteria-qualified-source-jev-review.md) remain the source of the
identified failure modes. Existing resource history is not silently upgraded.

## Chosen initial deployment profile

Provision a **new**, separately identified Fitz authority namespace and route
family, initially empty, with one fenced active writer, synchronous durable
commits, complete unfiltered history, and no event or metadata expiry, deletion,
compaction that removes logical records, or snapshot import while serving. A
namespace is infrastructure ownership and indexing, not an aggregate containing
operators, criteria entries, or any business child roster. Every resource has its
own stream and atomic expected-position append.

A separately durable control-plane lease/journal, outside the Fitz storage and
its rollback domain, allocates a non-reusable namespace identity and monotonic
recovery/ownership epoch. It records provisioning, active writer, recovery
state and approved source checkpoint. The deployment must make that record
strongly consistent, independently recoverable, access controlled, and
unavailable rather than silently recreated. The exact service is an integration
choice; neither the local Cassie volume nor process memory is assumed to have
these properties. The broker must establish an epoch fence *in the same
transactional storage authority used by every stream writer* before serving:
the active writer epoch is checked in every commit transaction, and newer-epoch
installation conflicts with/invalidates old transactions. An external lease
alone does not fence a partitioned old writer. Existing provisional writer-epoch
checks in the isolated broker are starting points, not proof of this guarantee.

The profile also requires exclusive server-side family routing, authenticated
broker access, storage integrity and a durable transaction outcome contract.
All other writers, importers, expiry paths and destructive administrative routes
must be disabled or participate in the same epoch and retention policy. The
qualification service denies activation if any property is unknown. The local
[compose.yml](../../../compose.yml) is explicitly development-only, pins Fitz
0.4.0, uses `FITZ_STORAGE_CLOUD_DURABILITY=background`, and provides no external
control-plane fence; it is **not this profile**. Select a truly synchronously
durable storage configuration only after verifying its actual implementation;
`StreamCommitMode.Sync` alone is not proof of cloud durability.

## Lifecycle and evidence

1. **Genesis.** The control plane reserves a globally unique namespace and
   records its initial empty provisioning before a broker can write. The broker
   creates a versioned immutable namespace-genesis/retention policy and an empty
   realm counter transactionally in that new authority. First resource creation
   atomically commits its immutable genesis identity, zero-based head and first
   records (or an explicit empty-resource creation record). The complete
   namespace keyspace is protected from external mutation and its storage
   transaction/integrity guarantees are qualified. Under those invariants a
   missing resource key can represent never-created only if the qualified
   complete namespace lookup establishes that fact; it is **not** inferred from
   an ordinary metadata fallback or empty scan. Missing, malformed, legacy,
   imported, truncated or inconsistent namespace/resource evidence is
   unavailable. Storage damage that makes a complete lookup indistinguishable
   from genuine absence is a qualification failure, not an accepted empty
   resource. Existing v0.4.0 namespaces require explicit migration and remain
   unqualified until that separate process proves all history.
2. **Retention.** The first profile rejects TTL at admission and disallows
   expiry, deletion and history-truncating maintenance for the qualified
   namespace, including administrative/import paths. Its qualified retained
   interval begins at zero. Records, scope index rows, counters and genesis
   metadata are atomically maintained; no isolated setting or head counter
   proves this. An unavailable/corrupt/missing page, index, metadata or record,
   or a storage integrity fault, invalidates the observation and qualification.
   No nonzero retained boundary qualifies full-history absence in this profile.
3. **Lifetime and common order.** Every live broker family gets a fresh random
   lifetime identity at startup and before any replay, including partial or
   failed replay. A successful observation binds namespace ID, control-plane
   recovery epoch, family, active writer fence and current lifetime ID. Area
   and realm lineage use the same namespace/epoch and explicit exact scope;
   positions from different scopes or epochs cannot be compared. Broker
   restart or reconnect invalidates old live handles. The process-local token
   by itself is not durable anti-rollback proof; the independent epoch and
   coordinated recovery below carry that responsibility.
4. **Observe and page.** On a single negotiated authenticated connection,
   authorize the exact route, establish current active writer/fence and resolve
   or fence any unknown relevant commit. Capture an immutable exclusive resource
   head and, independently, qualified area/realm heads from a storage-consistent
   observation. Return a bounded-lifetime handle bound to that exact connection,
   route family, scope, epoch, lifetime, retention policy and heads. Subsequent
   unfiltered pages carry the handle and requested offset; they deliver bounded
   byte/count pages, contiguous physical offsets including audits, and never
   later records. Disconnect, failover, replay, epoch/retention change, missing
   data, unauthorized request or expiry closes the handle as unavailable;
   never silently reopen under a new session. Authenticate/limit handle count,
   page size and TTL. With immutable no-expiry history, pages need not retain a
   long-lived storage snapshot if every page revalidates the same generation,
   fences and complete retained range, and its rows are checked against the
   captured head. Otherwise hold a bounded qualified snapshot or fail closed.
5. **Conditional append.** A qualified observation alone is advisory at the
   later write. A separate opt-in append supplies the exact resource, namespace,
   epoch/writer fence and expected exclusive head, plus a bounded event batch.
   In one transaction at the storage commit boundary validate namespace and
   writer epoch, resource genesis, current head and event IDs, then append the
   complete batch with resource/area/realm counters and indices. A conflict
   reloads; it never silently rebases the admission. Timeout/lost response is
   outcome unknown, never an empty or refused append. Recovery must observe a
   matching committed operation or append a genuine fence at the **same**
   expected position, then inspect the actual winner. A read/head check
   followed by the ordinary unqualified Portia append is insufficient. This
   guarantees only that target's atomic decision, not a cross-owner transaction.
6. **Realm checkpoint.** Provide independent unfiltered realm (and area if
   needed) scoped sessions: all physical positions through captured exclusive
   head, with exact source address, resource/area/realm offsets, immutable
   source identity and lineage, including audits and uninterested records.
   Realm positions are comparable only within the same qualified namespace,
   family, realm and epoch. Counter/index writes and records must serialize in
   the same committed transaction; a later commit cannot reuse an earlier
   position. Continuous processors atomically persist projection changes plus
   lineage-bound scope checkpoint. For event-driven workflows, durable
   immutable-intent per-operation intake must be resolved before checkpoint
   advance, but slow terminal child outcomes must not block unrelated intake.
   Causal comparison still separately verifies the exact participant event,
   payload binding and acceptance; a realm offset or checkpoint alone proves
   neither business outcome nor consumption.

## Coordinated rollback and restore

The smallest safe initial policy is **no online rollback or live snapshot
replay in a qualified namespace**. A controlled recovery stops admission and
all writers, fences the old epoch in the independent control plane and storage,
and invalidates live sessions before changing storage. Inventory Fitz, the
independent epoch journal, Cassie projection/checkpoint state, durable process
intake, and already acknowledged external effects. Restore only a verified
complete durable source history containing every acknowledged commit through
the previously recorded recovery checkpoint; otherwise keep the service
unavailable and perform explicit business reconciliation of missing intents,
receipts and external effects before any new admission. A backup's own counter
or a restored source's self-reported token cannot prove that no acknowledged
commits are missing. If the system cannot retain/recover that checkpoint and
its complete source prefix independently, it cannot certify zero-loss recovery.

Allocate a strictly newer control-plane epoch, install its writer fence, restart
all workers with new connection lifetimes, and either rebuild Cassie/process
projections from the qualified restored source or restore an independently
verified checkpoint explicitly bound to that exact lineage and prefix. Old
read handles, checkpoints and evidence are never compared numerically across
lineages. Previously committed, durable business decisions retain their source
identity and admission-at-time semantics; rebuild their derived state rather
than manufacture new decisions. Resynchronize external deliveries by exact
operation identity and reconcile unknowns. Re-enable only when continuity,
retention, routing, sole-writer and projection recovery are qualified. Raw
image replacement, uncoordinated snapshot restore and rollback of the control
plane itself remain disallowed. Enforcement (permissions, deployment stop,
lease/fence and restore runbook) must be demonstrated, not an operator promise.

## Exact code integration surfaces

- **Fitz broker**, isolated worktree
  `/private/var/folders/dg/rh2n13nx6cqgwpb8lkkxsgy80000gn/T/fitz-criteria-authority-ctyxw6l6`:
  extend `src/domains/stream/storage/keys_and_models.rs` for versioned
  namespace/resource genesis, protected retention and epoch metadata;
  `store/config_and_layout.rs` for explicit fresh provisioning (no legacy
  fallback), `store/compact_page_writes.rs` for atomic fenced event/head/scope
  updates, `store/sequence_and_filters.rs` and `watermarks_and_metadata.rs` for
  bounded qualified metadata and gap detection, and `store/maintenance.rs` to
  enforce no-expiry/no-delete policy. Extend `actor.rs`, `sink/model.rs`,
  `sink/mailbox_sink_impl/session_operations.rs` and `sink/snapshot.rs` for
  session/lifetime fencing and recovery barriers. Add exact observation,
  page and conditional-append dispatch in `protocol.rs`, `src/protocol/manifest.rs`,
  `src/protocol/stream_codec.rs`, `src/protocol/wire3/mod.rs` and
  `src/api/runtime_ingress/domain_*`. The existing reserved capability 3 and
  request 610 are unavailable-only; design and version their success/page
  schema together, assign any new request IDs without collision, add strict
  bounds and never advertise 3 before full broker and deployment qualification.
  The existing request core and errors are documented in
  `docs/development/qualified-resource-observation-wire.md`; no success body is
  currently implemented.
- **Fitz .NET SDK**, isolated worktree
  `/private/tmp/fitz-dotnet-criteria-source-ZBFZGB`: extend the opt-in
  `IStreamQualifiedObservationClient`, `QualifiedStreamObservationWire.cs`,
  `StreamClient.cs` and transport capability/session selection. Negotiate on
  the very connection that sends every request; no check-then-reconnect race.
  Parse bounded success/page/append bodies strictly, expose typed unknown,
  conflict, unsupported and cancellation distinctly, preserve the exact
  session/route/lineage, and never fall back to metadata. Old broker/SDK pairs
  remain unsupported. The current foundation intentionally always returns
  unsupported and has no granting provider.
- **Portia**, isolated worktree `/private/tmp/portia-qualified-source-20261105`:
  finish the provisional `Portia.Abstractions/Events/IQualifiedDomainEvent*`
  session and scope interfaces, adding a byte bound to exact-resource paging
  if needed, and a distinct qualified conditional-append contract carrying
  observation epoch/head. Implement an opt-in `Portia.Fitz/Events` adapter
  with strict SDK session affinity and independent event deserialization,
  contiguous offset and event identity checks. Existing `FitzEventStore.AppendAsync`
  sends expected offsets and synchronous commit but carries no qualified
  incarnation; do not equate it with the new append until the transactional
  epoch binding is proved. Keep ordinary reader semantics unchanged and
  register the adapter only on a qualified deployment profile.
- **Compliance**: replace or revise `ICriteriaFreshEntryHead` and
  `ICriteriaSourceCutCoverage`, whose current Boolean signatures do not bind a
  particular read session, with observation-bound evidence. Implement
  `ICriteriaParticipantCausalOrder` only from qualified same-realm facts;
  upgrade `FitzCriteriaExactSourceReader` and `CriteriaOrderedSourceEvent` so
  broker-reported realm offsets become evidence only when accompanied by a
  validated session and lineage. Bind durable source intake and runtime
  projection checkpoints to the scoped provider; verify participant outcomes
  separately. Supply qualified admission credential, policy and source adapters,
  then wire API/MCP/workers only when each required participant and recovery
  path is independently qualified. `Directory.Packages.props` currently pins
  Fitz .NET 4.0.0 and Portia 0.10.0; consume explicitly versioned qualified
  packages rather than assuming the isolated worktree changes are present.

## Staged qualification plan (not run)

1. **Store model and fault injection:** first creation vs legacy/missing/damaged
   metadata; empty first resource; first-append races; record/head/counter
   atomicity; fail-closed TTL/admin deletion; storage corruption, holes and
   byte bounds; transaction epoch conflict and old-writer split-brain. Cover
   actual sync durability and applied/unapplied unknown commit, including a
   delayed commit racing the target fence. Test with the real storage backend,
   not only fakes.
2. **Broker sessions and scopes:** wrong route/family, authorization,
   unnegotiated or unsupported capability, same-session affinity, disconnect,
   restart, replay before and after partial failure, epoch change, page/head
   changes, missing middle/tail, audit inclusion, bounded pages, multiple
   concurrent areas and exact realm order; prove no realm offset reuse under
   conflict/retry and no false availability after retention/integrity failure.
3. **Wire/SDK and Portia:** malformed/truncated/trailing/unknown wire values,
   size and cursor limits, negotiated unsupported/unavailable, capability
   negotiation plus connection replacement race, cancellation, no fallback,
   exact session binding, contiguous deserialization, source/realm lineage and
   append conflict/unknown reconciliation. Test both old and new endpoints.
4. **Compliance:** identity and permission revocation around the two-pass
   common instant, exact-source participant binding and publication ordering,
   claim/pin/abort races, durable signal before checkpoint, unrelated stalled
   participant, receipt pending on unknown, and incremental paged projection
   rebuild without all-child write-model gates or in-memory global sorting.
5. **Deployment and recovery:** provision unique empty authority; attempt all
   unauthorized mutation and TTL routes; verify writer lease loss and old-writer
   fence with actual storage; kill at each commit/ack/recovery stage; run
   coordinated restore, stale-replica and split-brain scenarios; prove complete
   acknowledged-prefix recovery, lineage invalidation, Cassie rebuild and
   external-effect reconciliation. Collect a topology/version-specific record;
   capability advertisement follows successful qualification, not a feature
   flag or passing unit tests.
