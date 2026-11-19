# Access-policy generation and admission

Status: **proposed; product and infrastructure qualification open**. This
design narrows the policy participant required by [criteria authorization](criteria-authorization-design.md),
[program creation](program-creation-criteria-design.md), and the
[evidence migration](access-admission-policy-content-migration.md). It does not
enable an admission provider, change existing authorization, or claim that an
event store is a qualified current-read source.

## Owner and invariant

Recommend one low-churn platform `AccessPolicyGeneration` owner for the current
access-policy generation. Its single invariant is that one exact active policy
content identity governs new admissions at a time. It owns no users, sessions,
accounts, roles, permissions, grants, deployments, node lists or child roster.
Existing per-grant and identity owners retain their own invariants.

The owner has a fixed platform identity and an absent or active state. The active
state is a monotonically increasing generation and a small immutable reference:
content schema, content identity, canonical SHA-256 and byte length. An
`AccessPolicyActivated` fact carries that reference, generation and activation
operation identity and trusted actor. Its exact source stream, non-reused source
lineage and physical position identify the activation. The bounded canonical
content is stored independently and retained immutably, including all historical
versions needed to verify admissions. The owner never embeds an expanding policy
history, participant list or full deployment state; its ordinary event stream is
low-churn and still grows. Do not claim bounded replay across arbitrary lifetime:
retention, stream lifecycle and any future rollover need separate qualification.

Activation uses a trusted administrative/deployment actor, a stable operation
ID, exact expected source position and a strictly greater generation. Exact
retry of an already committed activation returns the same outcome; a reused ID
with changed intent conflicts. An uncertain append remains pending until its
original position is resolved or a separately qualified fence wins; it is never
silently rebased. Bootstrap creates the first active generation exactly once by
the same trusted path, with no caller-provided authority proof. The canonical
policy bytes, schema and hash must already be durably verified; a missing or
unsupported source or unknown rollout state is pending. Undoing a policy is a
new higher generation, even if it restores old bytes. No previously used source
generation or activation generation may be reset or reused.

The activation handler must obtain a **trusted rollout authorization** from the
deployment boundary for that exact generation and content. This is a proposed
control-plane contract, not a user-supplied flag or proof from the owner itself.
It establishes that writers allowed to admit during and after activation support
the exact new policy; incompatible writers are fenced or excluded. Its loss or
uncertain state prevents activation. Its atomicity, durability, node identity,
in-flight requests, partition behavior and rollback protection need proof; merely
storing an activation event or a list of apparently healthy nodes does not prove
the barrier. Every deciding node independently reads the current authoritative
policy as an admission participant and rejects unsupported generation or digest.

## Content and scheme semantics

The policy artifact is a versioned, canonical, size-bounded document defining
the supported authorization semantics relevant to these paths: credential scheme
and identity binding, fixed veto applicability and completeness, operator
criteria admission, primary-admin and team/role/permission program paths,
permission names, scope rules and evaluator semantic version. Define canonical
encoding and digest over the actual bytes, not over a display label or unrelated
container image. The provider validates the immutable reference, length, digest,
schema and retained bytes; the evaluator must implement those exact declared
semantics. A hash string supplied in configuration or an unverified DTO is not
evidence of the bytes or evaluator behavior. Final artifact schema, trust root,
size bound and content-retention policy are **open decisions**. The provisional
aggregate slice caps the referenced artifact at 16 KiB on activation and replay;
the final schema and limit still require qualification. The aggregate checks only
the declared reference length and never loads or verifies policy bytes.

For the existing Bdgrz identity-only token path, policy may declare session,
account and global vetoes inapplicable only after the credential and security
owners explicitly establish that no such revocation or eligibility authority is
required for that scheme. The credential adapter must independently prove the
issuer, signature, lifetime and exact identity-to-user binding, and the
authoritative `UserIdentity` is a separate fixed participant. Unknown or required
veto applicability fails closed. External schemes need their own qualified
contract. This design creates no speculative session or account owner and makes
no claim of immediate session logout/revocation.

## Admission, recovery and replay

For each new criteria operator action or program creation/selection, obtain the
complete fixed, bounded credential/grant path and perform a qualified two-pass
read including the exact policy owner and all applicable identity, membership,
grant and veto owners. First reads precede trusted instant T and second reads
follow it. Non-resetting source versions must be unchanged; every relevant
mutation must advance its compared version. The policy bytes and declared
semantics must match the observed active generation at T. Policy activation
between the reads forces retry; activation after T follows the existing
admission-at-T rule. Unknown path alternatives, stale or unqualified current
reads, missing content, source resets or unsupported binaries are unavailable.
Record the bounded actor, operation, target, normalized intent, T, exact policy
generation, source version and content digest with the other participant
versions. Digest matching is necessary but does not prove source freshness,
credential validity, grants, all vetoes or the deciding implementation.

An independent verifier reopens the exact retained activation source generation
and position, checks the immutable content identity and bytes against the
recorded digest, and verifies the applicable historical implementation and other
authority evidence. Missing history remains unknown. A digest alone cannot
authenticate source, or prove a coherent admission. New claims and fenced
retries require the strict new evidence; exact already committed operations keep
their original evidence and outcome. Preserve legacy program events with neither
admission nor publication and structurally valid provisional admission-bearing
events on historical replay; never assign them an invented policy digest or claim
they have format-1 proof. Handling of unsettled legacy participant work is an
explicit unresolved recovery decision, as detailed in the migration note.

## Equivalent deployment-owned authority

An existing qualified deployment/configuration authority is an acceptable
alternative to the proposed domain owner if it supplies the same globally unique
non-resetting active generation and exact immutable retained policy content,
linearizable two-pass reads, activation ordering, historical verification,
activation authorization and a tested compatible-writer barrier or routing.
It must cover every admission writer and fail closed on stale observations,
partitions, rollback and unknown commits. The current repository has compiled
role tables, event-owned individual role permissions, local Compose and image
publication CI, but no demonstrated authority with those combined properties.
Do not implement a new owner if an independently qualified existing authority
already satisfies the contract; do not count a local binary version or config
value as one.

## Decisions and qualification gates

Product/security must select exact policy content and credential/veto semantics,
including whether identity-only is permitted; identify who can activate it and
the controlled rollback/revocation behavior. Infrastructure must establish the
authoritative source or deployment alternative, immutable storage and retention,
non-resetting lineage, current linearizable reads, trusted time, writer barrier,
mixed-version rollout, routing and failure recovery. Existing grant-path
discovery, credentials and other source providers require independent
qualification. Until these gates and behavioral tests pass, policy evidence and
new admissions remain unavailable. Aggregate event definitions alone cannot
qualify any of these infrastructure guarantees.

## Bounded Jev review

The [request](access-policy-generation-jev-request.json) and
[response](access-policy-generation-jev-response.json) record a focused review
using `jev-1.14.0`. Each number is the model's probability of **yes**, not an
implementation proof. It assigned 0.04 to the owner requiring a roster; 0.03 to
the owner proving store and rollout guarantees by itself; 0.25 to a V1-only
node authorizing against observed V2; 0.03 to an unbound digest proving
historical policy bytes; 0.93 to replaying a legacy program event without
claiming current proof; 0.07 to an unknown session veto establishing that none
is required; 0.06 to unqualified legacy pending work proving acceptance; 0.72
to the fully qualified deployment alternative; and 0.07 to the owner needing a
node roster. These support the stated boundaries only as advisory signals.

The broad common-instant question returned **0.42**, so that answer was
unresolved. A narrower [follow-up request](access-policy-generation-jev-followup-request.json)
and [response](access-policy-generation-jev-followup-response.json) returned
0.36 to a policy change being hidden by the unchanged qualified position, 0.34
to V1 being current at T, 0.03 to equal positions after a V2 commit, and 0.04 to
two stale cache reads proving currentness. The first two follow-up answers also
remain unresolved; they do not establish high model confidence in the two-pass
argument. Under the explicitly stated linearizability, complete participation,
non-reused monotonic positions and read ordering, the source reasoning is that
any intervening policy mutation would advance the second position; actual
deployment still must independently prove those premises. Do not infer them
from this review or enable admission on its probabilities.

The earlier broad question referred to multiple owners and asked about the
captured policy *state*; the follow-up mixed a policy value with a separate
source position and relied on the implied association between them. A standalone
[request](access-policy-generation-jev-version-request.json) removed those
ambiguities and asked only whether a single linearizable source's version at T
must be V1 when both surrounding reads return the same never-reused version.
The recorded [response](access-policy-generation-jev-version-response.json)
gave **0.62** for yes. This remains inconclusive and is not an approval. The
conditional reasoning and unqualified deployment premises above remain unchanged.

The focused [event-name request](access-policy-generation-event-jev-request.json)
and [response](access-policy-generation-event-jev-response.json) used
`jev-1.14.0` and returned 0.85 that `AccessPolicyActivated` describes the
activation transition, 0.03 that the event alone proves the rollout, and 0.07
that it requires a user or grant roster. The event is an owner fact only;
content verification, rollout authority and source freshness remain external
prerequisites. These model probabilities are advisory, not qualification.
