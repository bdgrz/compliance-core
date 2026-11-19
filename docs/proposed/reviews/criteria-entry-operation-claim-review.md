# Criteria entry operation identity claim review

Status: **partial; outcome and provider qualification remain open**. One
independent operation owner is addressed by `(edition, identifier, operation)`
and records at most one immutable identity claim. Human structural and summary
intents compete with trusted seed intents on that same source stream. Human
claims include the actual bounded intent, actor and initial trusted admission;
seed claims contain the compiled seed intent and exact edition binding without
inventing a human actor. Claiming is not a target decision or no-op outcome.

An exact human retry must have the same intent, actor and user, with current
admission bound to the captured target execution generation. A valid later
generation does not change the authored operation identity; the target still
requires its own verified generation and expected-position check. A seed claim
similarly retains its original generation as provenance; the seed proposal
verifier leaves a later generation pending until an exact target-fence recovery
proof exists. A different intent, kind, seed/human origin or actor conflicts.

Before creating an identity, the service checks syntactic shape, exact draft and
parent evidence, and a target preflight against its qualified current source
observation. It inspects an already existing operation claim after current
operator admission and before returning a state-dependent preflight refusal.
Any state-dependent preflight failure remains pending: even an absent claim at
one source read does not rule out an in-flight same-ID claim after that read.
Intrinsic malformed input is still rejected without a fact. The service then
reads the exact claim source, checks aggregate
position and source freshness, appends at its captured position, and re-reads
and requalifies the claim before attempting the target. An unknown claim append
remains pending. Target decisions cannot transparently rebase after their
captured source position changes.

These guarantees are conditional on a real current-source provider and actual
expected-position enforcement. The generic `ICriteriaFreshEntryHead` presently
has no qualified implementation. The operation owner deliberately has no
terminal or attempt-head implementation; in particular, a same-text summary
remains pending. A durable terminal fence must not permanently reject a valid
same-ID recovery or report a structural change failed before its target-side
fence is proven. Focused replay, collision and unknown-append tests were authored
but not run. Exact provider and concurrency behavior still require qualification.

## Jev review

The exact [request](criteria-entry-operation-jev-request.json) and
[response](criteria-entry-operation-jev-response.json) were returned by
`jev-1.14.0`. Probability of yes: a duplicate retry appends another claim
0.10; a newly authorized generation changes authored identity 0.25; structural
and summary intents can both claim one ID 0.18; seed and human claims can both
commit one ID 0.08; an immutable seed claim proves a later generation 0.07;
changed intent is disclosed before current admission 0.32; existing claims are
inspected before target-state preflight refusal 0.85; an exact claimed
operation's target refusal is already durable 0.13; an invalid point of focus
should claim an ID 0.13; unqualified source may allow a target mutation 0.36;
lost acknowledgement proves failure 0.11; an identity claim proves business
outcome 0.05; a stale target write silently rebases 0.07; the owner has an
operation or child roster 0.05; the code proves provider freshness 0.04; and
an absent claim excludes a concurrent later claim 0.10.

The 0.32 disclosure and 0.36 unqualified-source results are in the uncertainty
band. The source inspection shows the intended order and fail-closed branches,
but the model responses do not verify them. Provider-backed race tests and
authorization tests must establish those properties before claiming completion.
