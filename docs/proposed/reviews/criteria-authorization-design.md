# Criteria operator authorization at admission

Status: **product policy selected; source authority requires implementation
qualification**, 2026-11-05. The product owner directed the team to follow its
recommendations and check them with Jev. This record specifies when an operator
may admit a criteria operation; it does not assert that the current Boolean
authorizer or event provider already supplies the required source guarantee.

## Decision

Authenticate and authorize each new human criteria operation at an
**authoritative admission instant `T`**. At the same `T`, the sign-in identity
must be attached and not revoked, any applicable session must not be revoked,
the authenticated identity must be bound to the exact platform user, that user
must have a current active operator grant, and the current authoritative access
policy and every applicable global veto must allow the operation. Each fact
must come from its exact source owner or a proven equivalent authentication
contract. A signed, unexpired JWT alone does not prove current identity or
session authority. In particular, a revoked identity cannot authenticate a new
operation (PA-10).

A trusted authority service must establish those facts at one coherent instant,
not combine independently timed current-looking reads. The conditional
two-pass method below is one candidate. An identity, session, grant or policy
revocation that is still effective at `T` must be observed and deny the operation.
If the policy permits an explicit operator regrant before `T`, use the new grant
generation; a revoked sign-in identity itself remains terminal. A revocation that
commits after `T` prevents later admissions while effective but does not
retroactively cancel the already-admitted exact operation.

Admission concerns one authenticated identity, one operator and one operation.
It does not append one
authorization event per criterion operation to the operator aggregate and does
not require an operator roster or child list. A trusted internal admission binds
the authenticated issuer and subject, exact `UserId`, identity and applicable
session generations, exact operator grant generation, source positions and source
generations, authoritative policy generation, trusted `T`, client operation ID,
operation kind, target and complete normalized authored intent. A proof for one
operation cannot authorize a different target, operation ID, actor or changed
intent. The caller cannot supply or forge that admission evidence. The relevant
target's own durable intent or decision carries the binding; untrusted callback
data is not authorization. Never persist a bearer token or signing secret as
admission evidence.

An admitted operation may complete through source-backed system reactors after
the identity, session or operator grant is revoked. Each internal step must verify the exact
committed initiating intent and required participant facts. Being a system
actor alone does not authorize a new human-authored edit or publication. A
separate operation, including a new attempt after a terminal refusal, needs its
own fresh admission. The ordinary platform policy still governs who may grant
or revoke operator status and protects continuity.

## Delivery, recovery and retries

1. Authenticate the caller and establish the exact identity-to-user mapping,
   every applicable identity/session veto, current operator grant and policy
   generation together at `T` using a qualified authority proof. Create a
   trusted operation-bound admission from that proof. If any required source is
   inaccessible, stale, unknown or not qualified as current, do not admit.
2. Append the target's operation intent at its expected stream position, with
   actor, exact operation identity, normalized intent and admission evidence.
   Return an accepted/pending response only after that intent is durable. A
   successful source read alone is not a durable business operation.
3. If it is **definitively established** that the intent was never submitted or
   can no longer commit, a later attempt needs fresh current authorization. A
   pre-crash in-memory decision cannot be replayed as authority after revocation.
   If an append was sent and its outcome is unknown, the exact attempt remains
   pending. A later read that finds no intent does **not** prove that the old
   append cannot still commit. Reconcile the exact target and establish
   definitive provider settlement or the durable target fence below before
   starting a fresh attempt. A timeout, connection loss, empty read or no-op
   append is not such a fence.
4. A committed intent retains the original admission and can be recovered
   without giving the human actor renewed authority. Before exposing an exact
   retry's result to a human, authenticate and authorize that caller under current
   policy, including current identity/session validity; revocation can deny
   disclosure while the original admitted operation continues. Exact retry preserves its original actor, target, intent and
   terminal result; changed intent under the same operation ID is a conflict.
5. Publication has the same admission rule. A durable admitted publication
   attempt is not itself `Published`; its witness and edition transitions still
   have their own authoritative outcomes. Internal dispatch success, a
   no-event response and a timeout never establish those outcomes.

The coherent authorization decision and durable intent are intentionally not one
cross-stream transaction. Their business ordering is defined by `T`: a later
identity, session, grant or policy revocation does not undo an already-admitted
exact operation. If product policy instead requires authority to remain active
until eventual target commit, this policy is insufficient; that stronger rule
needs a qualified cross-owner ordering/fencing protocol and must not be inferred
from an earlier check.

### Conditional two-pass proof of one admission instant

One implementation may use the same fixed-participant technique described for
[program creation](program-creation-criteria-design.md#candidate-two-pass-coherent-positive-path),
provided its source contracts are established:

1. Authenticate the credential cryptographically and identify its exact issuer,
   subject and intended user. Select a **fixed** participant set: the exact
   sign-in identity owner and any applicable session/identity veto owners, the
   exact `PlatformOperator` owner for that user, the authoritative access-policy
   generation owner, and every other global veto the policy requires. If the
   identity-to-user mapping, veto set or policy generation cannot be established,
   fail closed; a claim or advisory index is not that proof. A platform
   authentication service can replace these reads only if its contract proves
   the equivalent current identity/session state at the same `T`.
2. First-pass read each exact participant from authoritative source, obtaining
   its current state, physical stream position and source generation. Each read
   must be fresh and linearizable, including a definitive source response for an
   absent owner. The observed identity must bind the authenticated issuer and
   subject to the same user whose operator grant is checked. A session token's
   user or identity claim alone is not that current binding. The Bdgrz session
   token's issuer/subject are the **session issuer and user ID**; its separate
   `user_identity_id` names the provider identity. Resolve and verify that exact
   identity's source binding to the same user and its originating provider
   issuer/subject. Do not interpret the session issuer/subject as if they were
   necessarily the provider identity's issuer/subject. An external token scheme
   needs its own explicitly verified issuer/subject-to-platform-user mapping.
3. After **all** first reads complete, sample trusted time `T`; then begin the
   second pass. Fresh-read the same fixed owners again. Each must have the exact
   same physical position and non-resettable source generation as its own first
   read. Every change capable of affecting identity, session, operator grant,
   policy or a veto must advance the compared position. Equal Boolean state is
   insufficient: revoke and regrant must change the position.
4. Evaluate every predicate at `T`, including raw credential not-before and
   expiration (`not_before ≤ T < expires_at`) and any other time-based veto. A
   token that expired before `T` is refused even if it was valid at request
   arrival. A token or grant that expires after `T` does not undo that admitted
   exact operation. A successful `T` is certified only after **all** second
   reads succeed, and the service attests their actual order, source identities,
   exact versions and policy generation. Retain this bounded evidence with the
   operation-bound admission.
5. A missing participant, mismatch, timeout, unknown source generation,
   unsupported policy generation or invalid predicate is not an allow. Retry
   with new reads and a new `T`, or report refusal/unavailable as appropriate.
   Do not reuse an earlier first-pass result as a fresh admission. There is no
   per-command actor history or all-operators scan in this positive proof.

The reasoning is conditional: for each participant its first read linearized
before `T` and its second after `T`; unchanged monotonic, non-resettable source
positions plus complete writer participation mean its relevant state was the
same at `T`. This establishes a common point across the fixed owners without a
cross-stream transaction. It fails if a source can return the same stale version
twice after a revocation, if a generation can reset or be recreated at the same
position, if mutable external content or an untracked veto can change without
advancing the compared position, or if `T` is not trustworthy. A runtime that
cannot satisfy these premises must fail closed or use an alternative proved
authority protocol. No such source-read guarantee is claimed merely from the
model result.

Built-in permissions and identity/session validation policy must come from an
authoritative current policy-generation source whose exact immutable content is
supported by the deciding node. A local binary version alone is not evidence
that the policy was current at `T`. During rolling deployment, a writer barrier
or qualified routing must prevent an old node from deciding under a new
unsupported generation; otherwise fail closed. If identity/session vetoes are
not fixed and enumerable, or a token issuer requires a revocation source that
cannot be read authoritatively, the candidate is incomplete. Define that
platform contract before claiming immediate revocation.

### Fencing an ambiguous attempt

The initiating intent has a stable client operation ID, immutable normalized
intent, target admission epoch and target stream. Its original append uses a
fixed expected physical position; an old worker must not silently reload and
rebase that admission to a newer position. Recovery reads the target and any
known intent identity. A committed matching intent is authoritative and retains
the original admitted actor even if their grant has since been revoked;
reconcile and continue it, subject to current authorization before disclosing a
receipt. A different committed intent under the same client identity is a
conflict.

If the intent is not found and the original append may still be in flight, a
trusted recovery process must settle the provider operation definitively or
append a **real target-owned fence**. When the operation and attempt token are
known, the fence binds that exact identity and generation. The fence and the
original append compete at the same expected position, including position zero
on a never-created target. If the original append wins, the fence conflicts and
recovery must reload and compare the **actual committed** actor, operation ID
and intent. If the fence wins, the delayed append cannot commit at its original
position; all target handlers must also reject the fenced generation after
rehydration. An empty event batch does not advance a stream and cannot fence
anything.

For an existing stream, another valid operation can advance the position before
either competitor wins. The old attempt still may not rebase; recovery reloads,
checks for a known exact outcome, and uses the target's current expected
position to write the relevant fence. A concurrent change causes another reload,
not permission to reinterpret the old authorization. When the original
operation and fingerprint are actually known, retain them so changed intent
cannot reuse that ID. If a target's lifecycle or event model cannot safely store
this fence, use a deterministic, separately owned operation attempt with the
same competing-append and replay guarantees; do not accumulate attempts in an
actor or parent roster. The actual owner/event contract and provider recovery
remain implementation qualifications, not guarantees of a no-op or timeout.

#### The original attempt is unknown

A crash can lose the earlier attempt token, client identity, payload or selected
expected position before any durable intent exists. Recovery cannot name or
fingerprint facts it does not have. With a known target and a **fresh,
authoritative** current position, it may instead commit a target-owned
**admission-epoch barrier** at that position, advancing a durable monotonic
target epoch. On a never-created stream the barrier competes at position zero.
All candidate writers must bind their admission to the target epoch and fixed
expected position they observed; after replay the owner refuses an old epoch,
and no worker may rebase an old authorization into the new epoch. A delayed
append at the competing position loses if the barrier wins. A writer whose
earlier position was already passed cannot commit there either. If an old
append wins first, the barrier conflicts: reload and inspect the actual
committed operation and intent before deciding what a recovering request means.
Other concurrent appends require reload and another conditional barrier; never
assume a source read or timeout canceled any submitted write. A lagging read,
writer that bypasses the epoch guard, or automatic stale rebase invalidates the
argument.

This recovery requires the original target to be known: for example, the
client's stable route identifies the same edition or entry, or a prior durable
operation claim binds the target. If it is not known, fencing a target named by
a later request does not fence a delayed append to some other stream. Recover the
original target from durable evidence or keep the old operation unresolved; do
not claim a global fence or scan unrelated records. If operation IDs are required
to be unique across multiple target streams, one target's history cannot enforce
that rule alone. A deterministic operation-ID owner or equivalent authoritative
claim must bind the ID to its target and intent, with its own competing-append
and ambiguous-commit recovery. Its absence leaves cross-target ID reuse open.

The barrier may be neutral and followed by a freshly authorized attempt, or it
may bind the **current authorized recovering request** and its normalized
intent. It must not assert that this was the unknown previous request. If the
old attempt wins, compare against what actually committed. If the barrier wins
and no prior intent was durably recorded, there is no evidence with which to
prove the current payload matches or differs from the lost payload. Enforce
changed-intent conflicts against a durable earlier intent or fence; do not
invent a lost fingerprint. If the product requires conflict detection even for
an attempted payload that was never durably recorded, a separate durable
intent-identity commitment is required and that commitment's own ambiguous
write must be handled by the same rules.

Only after old admissions are durably fenced or definitively settled without an
admitted intent may a new attempt generation start, under a fresh current
authorization decision. An exact retry can preserve its client operation ID
and unchanged intent while naming the new generation. Where a prior operation ID
and intent are durable, a different intent under that ID is a conflict; where
they were lost before any durable fact, their comparison is impossible as
described above. Unknown fence or append outcome remains pending until source
reconciliation. Internal recovery can perform the fence without granting a
revoked caller permission to see the result.

## Current source and required qualification

Current [PlatformOperatorAuthorizer](../../../src/Compliance.Core/Features/Tenants/Authorizers/PlatformOperatorAuthorizer.cs)
calls `IPlatformOperatorAccess.IsOperatorAsync`. The
[event-sourced implementation](../../../src/Compliance.Core/Features/Tenants/History/EventSourcedPlatformOperatorAccess.cs)
hydrates the global roster, loads its authority content and returns a Boolean;
it does not provide an operation-bound source revision, grant generation or a
documented current-read linearization proof. The proposed per-user
`PlatformOperator` owner is not yet implemented. Portia's RequestBus authorizes
before invoking a handler, not atomically with its later append.

The current Bdgrz session handler in
[ComplianceAuthenticationServiceCollectionExtensions](../../../src/Compliance.App/Features/Authentication/ComplianceAuthenticationServiceCollectionExtensions.cs)
does rehydrate `UserIdentity` after JWT validation and rejects an unregistered,
revoked or mismatched identity. That is stronger than checking only the signed
JWT. It happens separately from the platform-operator check, however, and does
not by itself establish a single common `T`, fresh linearizable identity reads,
all session vetoes, or the current access-policy generation. The registered
external resource schemes in that file do not establish an equivalent
current-identity callback merely by verifying JWT signature, issuer, audience
and lifetime. The required behavior for each scheme must be explicitly proved.

### Scheme-specific participant refinement

The provisional, unregistered admission evaluator supports a narrower Bdgrz
identity-only path when **both** independently qualified contracts expressly
permit it. The trusted credential resolver must certify that it verified the
Bdgrz session scheme and the exact signed `user_identity_id` to `UserId`
binding. The source-qualified current policy must identify its immutable
supported generation, declare its veto set complete, expressly enable this
scheme, and explicitly declare session, account and global-veto owners
inapplicable. Unknown applicability is unavailable. The evaluator then reads
identity, policy and the exact operator source in both passes; program paths
add their tenant, member and applicable grant owners. The exact principal
identity claim and source `UserIdentity` binding are independently checked.

The present Bdgrz token has no session ID and logout clears only a cookie;
identity revocation is checked by the existing session handler. This observation
is not proof that an external issuer has no additional revocation authority.
A required or unknown veto cannot be replaced by an invented negative fact;
the existing complete-participant path remains required for policies that
need those owners. A token whose policy requires a session but has no resolvable
session ID is unavailable. Identity-only mode is unavailable if any of the
three declarations is required or unknown.

Policy state, including the scheme and veto declarations, must be immutable
under its content identity and bound to the captured source version. Policy
changes must advance the compared source position, and rollout must fence or
route out nodes that do not implement the current exact policy generation.
These requirements apply before activation: no production credential resolver,
policy provider, source qualification or admission registration is supplied by
this refinement. In particular it does not establish immediate session
revocation, external-token eligibility, or source freshness.

Portia 0.10's `IDomainEventReader` promises contiguous committed resource
records; that contract alone does not establish that a completed read observed
every revocation committed before its linearization point. Fitz 0.4's tagged
stream spec describes committed watermarks, but that wording alone is not proof
of the required deployed current-read behavior across failures and replicas.
The implementation must provide and qualify fresh, linearizable reads for the
full fixed identity, session, operator, policy and veto set, plus an unforgeable
internal binding of their coherent decision to the target intent. If it cannot,
fail closed or implement a separately proven authority protocol; do not substitute
a stale projection, an arbitrary timestamp, or the present Boolean precheck.

An alternative common-order source decision may be investigated, but a
realm-wide checkpoint waiting on all earlier child work can recreate the
collection-wide dependency this domain rejects. Never claim that a source marker,
an unrelated projection's caught-up status, or a missing response supplies the
required current grant proof. Runtime migration from the legacy roster must
establish per-user authority and revoke behavior before retiring its existing
source; it cannot assume the new owner already exists.

## Race cases and Jev

| Case | Required outcome |
| --- | --- |
| Signed, unexpired JWT names an identity revoked before `T` | Deny admission; a cryptographic token alone does not override source identity revocation. |
| Credential names identity attached to user A but operation claims operator B | Deny; issuer, subject and identity source must bind the same user as the operator grant. |
| Identity is attached on the first read, revoked before `T`, and its second position advances | The two-pass proof fails; retry or deny after current source verification. |
| Identity/source and grant read true at separate times but never simultaneously | Deny or remain unavailable; separately timed true reads are not a common-instant proof. |
| Operator is revoked then regranted between reads, ending with the same Boolean state | The advanced source position invalidates the proof; retry at a new `T`. |
| A required session veto or current policy generation is unavailable | No admission; a stale binary, cached token or absent projection cannot supply the missing source. |
| Token expires after first reads but before `T` | Deny; raw expiry is evaluated at trusted `T`, not request arrival. |
| Identity or token expires after a certified `T` but before its admitted intent finishes | Continue the same admitted operation under the selected policy; later admissions and receipt disclosure require current authority. |
| Operator revocation commits before `T` and no valid regrant occurs before `T` | Deny the new operation; append no admitted intent. |
| Authorization read linearizes first; revocation commits before intent append | The one operation may finish under its admitted actor and intent; future admissions deny. |
| Process crashes after the read, with definitive proof no append was submitted or can still commit | No operation is durably admitted; a retry requires fresh current authorization. |
| Intent append response is lost and a source read is empty | Remain pending: the old append could still commit. Obtain definitive provider settlement or commit a real exact-attempt target fence before starting another generation. |
| Intent append wins a race with its recovery fence | Reconcile the committed admitted intent and continue it; the fence cannot overwrite it. |
| Recovery fence wins at position zero or on an existing stream | The old fixed-position append cannot commit and its exact generation can never be rebased or accepted later. |
| Another valid append advances an existing target first | Reload, check the exact operation and fence its generation at the new expected position; the old worker cannot rebase. |
| The old token and payload were lost before any intent committed | Use an admission-epoch barrier at the fresh target position; do not invent the old identity or fingerprint. Compare the actual event if the old append wins. |
| The epoch barrier wins before an old worker retries | The old position and admission epoch cannot be rebased; a current operation needs fresh authorization and the new epoch. |
| Barrier wins, and a caller supplies a current payload with no durable earlier intent | The barrier may bind the current authorized request, but cannot prove that an unknowable earlier payload matched it. |
| A fenced attempt is retried | Fresh authorization and a new attempt generation are required; preserve exact original intent under the same client operation ID. |
| A revoked user requests an old receipt | Enforce current disclosure authorization. Recovery may still finish an existing admitted intent. |
| Proof bound to operation A is presented for operation B | Reject; it cannot authorize a different ID, actor, target or intent. |
| Current-grant read cannot establish freshness | Do not admit; a Boolean or historic grant alone does not prove current authority. |

The exact [Jev request](criteria-authorization-jev-request.json) and
[response](criteria-authorization-jev-response.json) used `jev-1.14.0`. For
the stipulated cases, Jev returned 0.98 that an operation admitted before
revocation may finish, 0.02 that an admission after revocation may proceed,
0.95 that a retry after a pre-commit crash needs fresh authorization, 0.94 that
an unknown append needs reconciliation, and 0.02 that a proof for another
operation is reusable. It returned 0.04 that the earlier Boolean check proves
authority at later commit and 0.09 that the policy establishes deployed
linearizable reads. These are conditional review signals, not provider proof.

The separate [identity request](criteria-admission-identity-jev-request.json),
[response](criteria-admission-identity-jev-response.json),
[explicit-policy follow-up](criteria-admission-identity-followup-jev-request.json)
and [response](criteria-admission-identity-followup-jev-response.json) retain
the confusing signals. The first, underspecified revoked-JWT question returned
0.84 that admission could proceed; the follow-up explicitly supplied PA-10's
revoked-identity prohibition and returned 0.04. The common-`T` question
returned 0.55 even with the stated two-pass assumptions; that result is
inconclusive and is not evidence of the algorithm's correctness. The simple
interval argument above explains the conditional proof. In the explicit
examples, Jev returned 0.14 that a changed identity position satisfies equal
positions, 0.07 that identity and grant held together in the disjoint-time trace,
0.02 that equal cached values prove freshness, 0.10 that hypothetical
linearizable reads prove the deployed provider has them, and 0.97 that an exact
operation admitted before later revocation may finish under the selected policy.
The mixed scores remain advisory and do not change PA-10 or establish runtime
guarantees.

The follow-up [fence request](criteria-authorization-fence-jev-request.json)
and [response](criteria-authorization-fence-jev-response.json) address the
previously omitted in-flight race. Jev returned 0.09 that an empty read proves
the old attempt can never commit, 0.12 that a no-op append fences it, 0.93 that
a committed old intent is authoritative when it wins, 0.94 that fresh authority
may start a new generation after fencing, and 0.06 that the examples prove the
deployed provider's behavior. It rated the delayed-old-append possibility 0.71;
that score is inconclusive, while the explicit unfenced trace demonstrates the
risk. These answers do not replace an executable expected-position and recovery
qualification.

The [lost-attempt request](criteria-authorization-lost-attempt-jev-request.json)
and [response](criteria-authorization-lost-attempt-jev-response.json) test a
barrier without access to the old identity or payload. Jev returned 0.03 that
an empty source reveals the old intent, 0.02 that the current request proves an
unknown old payload matched, 0.87 that recovery must reload the actual winning
intent, and 0.05 that an old admission may be rebased after the stated epoch
guard. It rated the old position committing after the competing barrier at
0.21, an inconclusive result. The target's expected-position and epoch behavior
must be verified from provider source and executable competing-append tests;
the model does not prove those contracts.

Focused implementation qualification must cover identity/session and grant
changes before, during and after the two passes; issuer/subject-to-user binding;
credential expiry exactly at `T`; session and policy vetoes; current policy
generation and rolling deployment; source ABA/reset and stale replicas;
read-versus-revoke ordering,
same-ID exact retry and changed intent, authorization after revocation, delayed
intent append, crash before append, commit-unknown reconciliation, untrusted
proof injection, system-callback provenance, and native provider recovery.
