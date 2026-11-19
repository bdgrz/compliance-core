# Criteria publication and program creation: acceptance contract

Status: **selected design; implementation and qualification outstanding**,
2026-11-05. This is a checklist of observable behavior for the feature, not a
claim that the current source or provider already satisfies it. Product choices
are summarized in [selected decisions](criteria-product-recommendations.md).

## Source and admission

- Only an authenticated platform operator can admit a new edition, label,
  structural edit, or publication operation. Admission uses a current,
  source-authoritative read of the actor's exact operator grant. The trusted
  target intent binds its grant generation/version and exact operation identity.
- A revocation ordered before admission prevents a new operation. One ordered
  after successful admission prevents further admissions, but does not cancel the
  previously admitted exact operation. A crash before a durable intent requires
  fresh authorization; an unknown append is reconciled against the same identity
  before a retry. An empty read does not establish that an old append cannot
  arrive: obtain definitive settlement or commit a target-stream fence at the
  competing expected position. A fenced execution generation cannot rebase;
  a new generation needs fresh authorization and keeps the client identity.
  Current authorization controls disclosure of old results.
- Edition, entries, intents and the publication fact use a configured common Fitz
  Stream ordering scope. Confirm durable source reads and routing with the pinned
  deployment; do not rely on best-effort notifications or compare resource and
  area cursors. Loss, incomplete retention, gaps, and ambiguous commits must be
  visible and recoverable.

## Edition, entries and publication

- One `(EditionId, identifier)` owner covers both entry kinds and every
  incarnation. Concurrent differing edits cannot both win the same expected
  revision. Exact operation retries preserve the result; changed intent
  conflicts. Definitive refusal or a durable token fence precedes reuse; an
  empty append, lost reply or elapsed time is not a fence.
- Removing a draft criterion makes points of focus bound to its exact incarnation
  ineffective without writing to them. Re-adding the identifier does not restore
  old references. Individual repair is possible in the same draft. Published
  reads evaluate the point and its exact parent at the same immutable edition
  cut, even if either record changes later.
- An edition orders its publication attempt against rename and abandonment. One
  exact accepted criterion is pinned as witness. Pin-versus-removal orders on the
  witness owner; unknown remains pending. Terminal publication and abort compete
  on the edition owner. The abort commits first and leaves cleanup pending; an
  exact token-specific witness fence is then recorded and acknowledged before
  another publication or abandonment. Both a terminal fact and a later pin
  arrival trigger exact-attempt recovery. The target fence must reject a delayed
  first pin, and an old duplicate cannot reacquire a released pin. A published
  witness is not released into an unfenced state.
- `CriteriaEditionPublished` is the only structural cut. The publication write
  checks its exact attempt, current confirmed label and one witness; it does not
  wait for every entry or a shared checkpoint. The committed cut position is
  discovered afterward. A pre-cut edit can settle later: accepted affects the
  cut at its intent position, while refusal leaves the preceding accepted state.
  A post-cut edit is not an accepted change to that edition.

## Labels and program creation

- One claim per normalized label orders competing holders. Initial label refusal
  keeps the client-created edition ID; a distinct operation chooses another
  label. A rename reserves and confirms its destination before releasing the exact
  old generation. An old release cannot clear a new holder. A delayed reservation
  after cancellation or refusal triggers its own source-backed cleanup.
- A publication attempt excludes rename and abandonment until its exact terminal
  outcome. A lost response is pending, not successful publication. An abandoned
  initial request cannot be activated by a delayed confirmation. Immediate
  rename back to a label awaiting this edition's old-generation cleanup reports
  cleanup pending rather than reusing the stale generation.
- Program creation requires client-supplied tenant, program and edition IDs,
  current organization-wide authority, and exact irreversible publication
  evidence. A different normalized edition/name/plan under the same program ID
  conflicts; an exact currently authorized retry returns the original response.
  Competing and ambiguous appends reconcile the actual winner. An edition's
  unrelated pending entries do not block creation, but are never called absent.
- One candidate organization-wide grant path is verified with two fresh,
  linearizable reads of every fixed participant around a shared instant `T`.
  Equal monotonic positions establish overlap only if source reads are current,
  stream positions cannot reset, every applicable veto is included, and raw
  expiry and an authoritative current permission-policy generation are evaluated
  at `T`. The policy owner is itself a twice-read participant, and the local
  binary must implement that exact generation. A change observed in either pass
  fails closed or retries. Every authority-changing fact
  must advance the compared stream position or be immutable content verified by
  that version; mutable out-of-band or audit-only state cannot invalidate an
  equal-position proof. Exact issuer/subject, identity/session revocation and
  all tenant/member/team/role/scope join identities must be checked at `T`; two
  active but unrelated records cannot form a permission path. The authority
  service attests its read order, since stored versions alone do not prove it.
  A candidate-discovery index
  cannot authorize or prove absence. Exercise revocation, ABA, disjoint validity
  intervals, cross-realm reads, expiry across UTC midnight and mixed binary
  policy versions. The source and deployment must qualify these assumptions.
- An unknown first `ProgramCreated` may race a `ProgramCreationAttemptFenced`
  event on the same initially empty stream. If creation wins, recover that exact
  original result; if the fence wins, stale attempt generations cannot rebase,
  the fence alone does not create a program, and an authorized successor uses the
  same `ProgramId` and bound intent. If no prior intent was durable, the fence
  binds the authorized recovering request; it fences every stale append at that
  position and does not manufacture evidence of the lost request's intent. If a
  prior creation wins, compare against its committed intent instead.
- A legacy program event with no edition remains `legacy_unknown` until an
  explicitly attributed selection. Preserve historical retry semantics and
  version the API/MCP and event cutover. Do not fabricate an edition in replay.

## Read coverage and seed migration

- A source-backed index processes the ordered scope incrementally and commits
  individual rows with its checkpoint atomically or equivalently recoverably.
  Replaying duplicates and recovering an unknown checkpoint cannot lose a row
  or apply it twice. An index is an ordering witness for a write only after its
  exact source coverage, identity, retention and recovery are qualified.
- A catalog page binds the edition cut and index generation. Use keyset pagination
  or streaming; do not load the entire catalog into memory to sort before paging.
  Before issuing a stable cursor, prove discovery of every relevant pre-cut
  identity, or bind it to an immutable materialization and invalidate it if its
  key set changes. After identity discovery, stable pages may include pending
  identities; a pending item settling between pages must not be skipped behind
  the cursor. Bind each walk to a consistent materialization generation and
  stable identity order; an unresolved structural change that might match a
  category filter is surfaced as pending rather than excluded. Settlements may
  appear in a later refreshed generation. Rebuilds remap the marker by source
  identity and invalidate unavailable-generation cursors. This read-side gate
  never gates publication.
- Coverage through the cut and terminal outcomes for its pre-cut intents are
  separate requirements. A caught-up checkpoint, projected zero or hash alone
  does not prove complete coverage. Readiness and applicability expose unknown
  until their required exact entries and source coverage are established.
- Preserve the existing seed edition ID and the disclosures for partial
  points-of-focus coverage and unsupported Privacy lifecycle. The current
  in-memory seed and `IsComplete` flag are not by themselves durable publication
  authority or evidence of full framework support. Seed migration and rolling
  deployments must prevent a changed structural catalog from silently retaining
  the same edition identity; verify source compatibility and individual seed
  facts without creating a catalog-sized aggregate or payload.

## Verification before acceptance

Implement focused tests for the interleavings above, including empty-stream
fences, competing appends, delayed first pins and reservations, changed-intent
retries, cancellation and publication races, permission revocation, unknown
commits, and projector crash/replay. Exercise API, MCP and read-model behavior,
legacy replay, stable pages during late settlement, and the actual configured
Portia/Fitz/Cassie path. Run the repository's required build and unit gates when
the authorized implementation chunk is complete. A Jev score or a specification
alone is not evidence that these requirements passed.
