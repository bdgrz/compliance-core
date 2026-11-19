# Candidate primary platform operator protocol: adversarial review

Status: **historical candidate review; replacement-first and two-primary
policies approved, provider guarantees unqualified**, 2026-11-05. The user subsequently
approved replacement first and at most two confirmed or pending primaries. The current
[protocol and alternatives](platform-operator-primary-protocol.md) distinguish
that decision from the conditional two-seat candidate reviewed here. This review
does not assert that a provider supplies the required guarantees. The current
implementation uses the [PrimaryPlatformOperators aggregate](../../../src/Compliance.Core/Features/AccessControl/Aggregates/PrimaryPlatformOperators.cs)
and [bootstrap service](../../../src/Compliance.Core/Features/Tenants/Services/PlatformOperatorBootstrap.cs).

## Conditional safety argument

After bootstrap, assume the seat stream has a confirmed seat whose exact target
has a committed active grant and matching current mark; a marked target refuses
revocation and all mark-removal routes verify a committed exact seat release.
A release can commit only while a *different* confirmed seat exists at the
same seat-stream position. Two concurrent releases of A and B prepared at P=p
compete at that exact position: at most one commits; the loser must reload P and
refuse release of the final confirmed seat. The winning release can later cause
an exact-generation unmark on its own operator stream. A replacement remains
protected because no target may unmark it without its own committed seat
release. This proof depends on conditional append, complete participation by
all writers, exact committed-source verification, and no source reset or stale
read masquerading as current. It is not supplied by the seat count or by Jev.

A target-owned mark/revoke race is similarly local: both attempts compete at an
exact target stream position. If mark wins, revoke must reload and refuse while
marked; if revoke wins, mark must refuse or remain pending. A timeout or absent
read does not determine the winner. An already accepted mark must not be
reported as a fresh current mark after a later unmark or generation change.

## Gaps and decisions before implementation

1. **Bootstrap authority and single completion.** Decide who selects and
   authorizes the first exact user, how a candidate's eligibility is proved,
   and what makes the platform ready. A reservation on P and a target's atomic
   grant-plus-mark are distinct commits; persist the exact bootstrap operation,
   target, generation, actor/provenance and target outcome. Ready only after P
   confirms the exact committed target acceptance. A timed-out append followed
   by an empty read is not proof of absence: an old grant/mark could still
   commit after a different seed starts. Resolve the original write or commit a
   target-owned fence competing at its original expected position before moving
   to another candidate. Concurrent seeders must contend on P and recover the
   actual winning immutable intent. Specify a durable, non-resetting completion
   marker and eliminate every later configured-ID regrant route, including
   restarts and restored deployments.
2. **Seat confirmation and release evidence.** Define exact request and event
   identities, source addresses and generations for reservation, target mark,
   confirmation, release and unmark. P must independently read the exact
   committed mark and verify user, designation, target stream, grant episode
   and acceptance, not trust a callback ID. Every unmark path must independently
   verify P's committed release of that exact target and designation; no
   caller-provided event ID is authority. Deny a release based on pending or
   merely historically confirmed replacement, and require the replacement to
   remain protected under the protocol. Define whether seat generations are
   globally unique or have non-reusable epoch/counter identities across reset.
3. **Refusals, cancellation and delayed marks.** Specify a durable terminal
   target fact for each refused or canceled generation, so a late mark cannot
   succeed. If the target outcome is unknown, the reservation stays pending;
   a fresh authorization or new operation does not settle an in-flight old
   append. Exact attempted position, no rebasing, target-side conditional fence,
   winner inspection and retry after conflict must all be explicit. If a mark
   committed before the fence, recover the actual mark and complete or
   compensate it; do not fabricate a refusal or simply free the seat.
4. **Unmark and redesignation.** Release of A generation g1 and a later
   designation g2 require exact-generation target checks: delayed unmark g1
   must not clear g2. There is also a liveness decision: if A remains marked
   g1 until cleanup, a g2 mark may be refused. Reconcile the old verified
   unmark before reserving/retrying g2, or specify an atomic target rebind with
   proven authorization; never treat a no-event response as proof cleanup
   completed. Define how pending cleanup is discovered after crash and how an
   indefinitely unavailable participant is escalated without stalling unrelated
   operator commands.
5. **Recovery and authorization.** Choose durable bounded per-operation
   signal/intake identities, a discoverable sweep and immutable-intent retry
   behavior. Source checkpoints must not advance before durable intake is
   settled; a stuck participant must not block a shared checkpoint. Human
   requests need current source-qualified identity and operator admission,
   while system callbacks need exact committed initiating facts. State which
   admissions survive actor revocation and which require current authorization
   to disclose receipts. Preserve the only primary during recovery; a locked-out
   primary's identity recovery and exceptional disaster recovery require their
   own explicit authority, not a hidden automatic regrant.
6. **Migration and cutover.** Current roster writers and the new per-user
   owners cannot both remain independently authoritative during an unsequenced
   scan: a revocation after a scanned snapshot could be missed and a revoked
   user recreated as active. Choose a qualified writer barrier plus consistent
   source cut and replay/reconciliation, or a fully specified durable dual-
   write protocol with authoritative ordering and recovery. Establish and
   confirm a protected primary before switching admission, validate exact grant
   epochs and operator membership at the cut, and prevent legacy write/read and
   bootstrap routes from reactivating old grants after cutover. Resolve
   concurrent writes, partial migration, restart, rollback and restore before
   declaring success. The former roster's immutable authority content must be
   verified against its committed reference, not a projection or configuration.
7. **Bounded aggregate histories.** Two current seats bound current state, not
   event-stream lifetime. Portia replays the full aggregate stream; quantify
   expected churn and specify a safe rollover/retention/restore protocol before
   claiming an actual aggregate-history bound. Per-user operator streams also
   need a real lifecycle/churn assessment. Existing `PrimaryOrgAdmins` is a
   useful illustration of replacement-first decisions, not proof of this
   protocol: its [seat decisions](../../../src/Compliance.Core/Features/AccessControl/Aggregates/PrimaryOrgAdmins.cs)
   and [member mark/unmark](../../../src/Compliance.Core/Features/AccessControl/Aggregates/Member.cs)
   do not establish platform operator source verification or cross-stream
   recovery guarantees.

## Bounded Jev review

A single `jev-latest` System One request used only the abstract candidate and
specific traces above; no credentials or user data were included in its state.
The TypeSafe API key was used for authentication. The exact
[request](platform-operator-primary-protocol-jev-request.json) and
[response](platform-operator-primary-protocol-jev-response.json) are retained;
the returned model was `jev-1.14.0`. Each Noul is the probability of **yes**.

| Question ID and exact question | Yes |
| --- | ---: |
| `both_releases`: In state.race and state.cas, can both releases validly commit and leave no confirmed seat? | 0.16 |
| `bootstrap_risk`: In state.bootstrap and state.cas, can the supposedly abandoned first write still commit after the second seed starts? | 0.95 |
| `stale_unmark`: In state.redesignation, can the delayed g1 unmark remove g2 if the target atomically requires an exact current generation match? | 0.09 |
| `unfenced_refusal`: In state.failure, can a delayed mark contradict the durable refusal without a target-owned terminal fence? | 0.94 |
| `unverified_id`: In state.untrusted, does the event identifier by itself establish the matching committed operator mark? | 0.04 |
| `migration_race`: In state.migration, can a concurrent legacy revocation be missed by the migration and leave an incorrectly active new grant? | 0.92 |
| `cleanup_liveness`: In state.cleanup, can the attempted new designation remain blocked until the old unmark is completed or another explicit transition is designed? | 0.90 |
| `history_bound`: In state.history, does the two-seat state limit bound the lifetime length of the event stream? | 0.08 |

These probabilities are adversarial review signals, not proof. In particular,
0.16 for both releases reflects uncertainty in the model, while the one-stream
expected-position trace rules out two commits *only* under the explicitly
stated compare-and-append and revalidation assumptions. Provider freshness,
non-resetting generations, durable fences, writer coverage and executable race
and restore tests remain unqualified. Replacement first and at most two
confirmed or pending primaries are now approved; the unlimited-primary keeper
subset is not selected.
