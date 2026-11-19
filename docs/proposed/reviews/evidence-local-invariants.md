# Evidence: local decision refinement

Status: **candidate, not implementation-ready**, 2026-11-05. This review
supplements the [platform and evidence boundary review](platform-evidence-boundaries-review.md)
and the [evidence context](../contexts/evidence.md). The accepted
[M0-D16](../decisions/m0-d16-evidence-handling.md) rules remain in force.

## Independent relationships and fulfilment

An evidence request's fulfilment can cite one exact available artifact as its
nonempty witness. Each additional request-artifact association is a focused owner;
each artifact-to-exact-record-version support statement is separately owned. No
request or artifact stream receives an event for every independent support link.
Read-side lists group outcomes, but a projected count or list is not proof that a
batch completed. Unknown, refused and accepted links must remain distinguishable.

This does not by itself establish availability at fulfilment. Disposal or
quarantine can race the check; request cancellation can race a link admission.
Name the owners and authoritative ordering, idempotent operation identity,
participant result, and recovery before coding. A sole anchor proves neither
that all the other artifacts in an offered group were accepted nor that their
support links exist. Whether the previous multi-artifact UI promised all-or-none
acceptance needs explicit product review; this candidate is not silently
equivalent. A whole-child scan or parent-held list is not an approved remedy.

## Actual release and disclosure

M0-D16 requires eligible evidence for sharing and a record of every disclosure.
Approval of a redaction, inspection of its derivative, present permissions and
release of bytes are distinct facts. A clean derivative is not automatically
an approved redaction. An old availability or permission read does not order a
later release against disposal, quarantine, reclassification or revocation.

The necessary candidate is a **per-artifact release decision**, naming the exact
content hash, recipient, purpose and effective handling class, with a qualified
ordering/fencing protocol against the changes that can invalidate that release.
It must cover the actual release point; a log reactor after transmission cannot
authorize bytes already sent. The present repository proposal does not establish
the required transaction, fence, lease or revocation semantics. In particular,
the meaning of a permission change after streaming begins remains open.

The delivery and disclosure owners also need a durable identity for each actual
release and a recoverable outcome when the sender times out after some or all
bytes are sent. A timeout is not a refusal and does not establish that a recipient
received nothing. The same occurrence must not emit duplicates on exact retry;
whether a genuine retransmission counts as another disclosure is a product
decision. Failed disclosure recording remains visible and recoverable rather
than silently turning an unlogged external release into success. No parent holds
the list of artifacts released by a package.

## Hold placement versus physical deletion

`DispositionDecided` is a necessary retention authorization, not proof that a
hold cannot become active before the physical content is irreversibly deleted.
The earlier RT-06 reading allowed the Evidence owner to delete later without a
new hold check, but M0-D16 protects held artifacts. How a hold ordered after the
authorization affects an outstanding deletion is unresolved; a fresh read is
also insufficient because the hold may race the deletion. The conservative
candidate keeps deletion pending until a proven authoritative ordering or fence
covers that hold and physical byte deletion, with explicit recovery for unknown
results and crashes. Do not claim the current provider already supplies it. The
[Retention proposal](../contexts/retention.md) retains the separate open question
of a retention-date extension and the scope/activation of an individual hold.

## Jev evidence

Exact synthetic [request](evidence-local-invariants-jev-request.json) and
[response](evidence-local-invariants-jev-response.json) use `jev-1.14.0`.
Probability of each stated proposition: one anchor avoiding a request list 0.93;
that anchor proving atomic batch acceptance 0.02; a stale availability race 0.92;
a late audit record preventing an already completed release 0.05; an ambiguous
timeout guaranteeing no bytes were received 0.03; clean inspection proving
redaction approval 0.02; and a parent stream growing per support link 0.92. A
random disclosure identity causing duplicates on retries scored 0.78. The
per-artifact fence answer was 0.92 **conditional on the supplied premise that it
atomically orders eligibility and release**; no model score establishes such a
primitive, provider guarantee, or safe delivery protocol.

No implementation, build, tests or deployment are claimed by this review.
