# Local write invariants

Status: **working design guidance**, 2026-11-05. These patterns explain how to
review a proposed write model. They are not evidence that a particular
cross-stream ordering primitive is available or that a proposed product change
has been accepted. The [context conventions](contexts/conventions.md) and each
context's decisions remain authoritative about its specific rules.

## The decision boundary

Ask what one command must decide, what fact makes that decision valid, and which
owner can make that fact authoritative. An aggregate has its own business
identity and invariant; its stream is not a list of related records or their
outcomes. A parent, batch, campaign, edition or time window is not a reason to
put independent children in that stream. Dividing the list among streams does not
make an all-child write decision local.

Portia hydrates the entire aggregate stream. Review both current state and the
events replayed over the record's lifetime, considering the expected volume and
churn of its own business decisions. A small, low-churn aggregate can reasonably
retain its history when that history serves its invariant and retries; it does
not need an artificial lifetime cap or a more complicated replacement. For
example, `PrimaryOrgAdmins` protects one or two primary seats and changes only
when those designations change. That is distinct from an owner accumulating
ordinary child memberships or outcomes for an independently growing population.

## Patterns to consider

| Business need | Local direction | Condition to establish |
| --- | --- | --- |
| One owner for a unique business identity | Give the claim its own deterministic identity; competing claims contend on that owner. | Specify normalized key, scope, release/reuse and exact retry semantics. A projection lookup alone does not exclude concurrent claims. |
| A record must reference one valid parent | Validate the exact parent and bind its identity or incarnation to the record. Choose a lifecycle in which later parent changes cannot invalidate an accepted child, or define explicitly how the relationship's validity changes. | A read alone does not serialize a child creation against parent retirement or removal. Specify authoritative ordering and late-command behavior. |
| At least one qualifying record must exist | Make the decision cite one qualifying, durably accepted witness. | The witness must remain valid at the decision's authoritative point. A single witness cannot prove unrelated children are valid. |
| Review or decide one item | The item owns its decision, actor, exact subject version and lifecycle. | Concurrent edits, review permissions and retries need local checks and any required authority from the subject. Grouping items on a screen does not make their decisions one aggregate. |
| Show progress across items | Derive a list or status from individually owned facts; page or stream the results. | State freshness and source coverage. A lagging count cannot prove that no pending item exists or authorize an irreversible action. |
| Capture an exact point in time | A decision may name an authoritative source position or version if that is genuinely the business fact being decided. Read-side materialization can enumerate the facts visible at that position. | Qualify ordering, retention and late writes. A marker, hash or projected count does not prove that all children were examined, approved or complete. |
| Coordinate a specific cross-owner change | Limit the business operation to its actual participants, with attributable accepted/refused outcomes and explicit pending, retry and recovery states. | A successful advisory read or dispatch is not acceptance. Do not turn one decision into an inbox or ledger for a growing population. |

These patterns can compose only when their assumptions compose. For example, a
criterion's accepted parent reference and a publication's nonempty witness do
not by themselves prevent an edit from racing publication. Likewise, individually
closed actions and a projected count do not automatically authorize a recorded
finding closure that promises every action was terminal at that instant.

## Decisions, derived status and existing promises

A recorded business decision and a displayed aggregate status answer different
questions. An operator can approve a specific record while its work items retain
independent lifecycles; a read model can show the outcomes known so far. If the
product instead promises that a recorded closure proves *every* related item was
terminal at the moment of closure, merely changing that closure to a projected
status weakens the promise. Do not present the two designs as equivalent.

Where an accepted rule appears to require a collection-wide write, document the
original guarantee, the smallest alternative decision, how users observe ongoing
work, and the changed guarantee. Keep the alternative provisional until its
product semantics and authoritative concurrency protocol are resolved. Separate
an actual customer deliverable, which may need all its bytes streamed, from an
aggregate owning a manifest of all those bytes or children.

The [domain boundary review](reviews/domain-boundaries-review.md) lists the
specific conflicts, evidence, and implementation-order implications.
