# Criteria draft correction and parent references

Status: **product direction selected; authority protocol requires qualification**,
2026-11-05. The product owner directed the team to follow its recommendation and
evaluate it with Jev. This record selects draft correction through independent
entry incarnations; it does not assert that Portia or Fitz has already proved
the publication and recovery protocol. It refines the open CR-09 lifecycle in
[Criteria](../contexts/criteria.md), subject to reconciliation there by the
coordinator. Published-edition immutability and the one-parent rule in
[M0-D02](../decisions/m0-d02-criteria-content.md) remain in force.

## Decision and user experience

Each entry has one owner and address `(edition, identifier)`, shared by criterion
and point-of-focus kinds and by every incarnation. Changing kind, category or
parent, and removing or re-adding an identifier, cannot create a second owner or
bypass that identity's uniqueness. Its structural state changes through immutable
incarnations. An incarnation of a criterion fixes its identifier, category and kind. A point-of-focus
incarnation fixes those fields and refers to one exact parent criterion
incarnation in the same edition and category. User-written summaries can be
revised independently under the existing content rules.

A draft operator may remove or replace a criterion incarnation. Removal changes
that criterion only: it does not read, write or fan out commands to points of
focus. A point of focus is **effective at a particular cut** only when its own
incarnation is included at that cut and its exact parent criterion incarnation
is included there as well. The parent incarnation is immutable, so its identity,
kind, edition and category cannot drift. A parent removal therefore makes its
old points of focus ineffective by this rule. Re-adding the same identifier
creates a new incarnation and does not reactivate references to the old one.
The operator may repair each affected point of focus by explicitly revising its
parent reference, or remove that point of focus. Correcting a draft stays in the
same edition; no child-sized payload, collection owner or all-child write is
required.

This intentionally replaces the earlier proposed rule that refused parent
removal while any point of focus referred to it. The interface must explain that
the removal makes old references ineffective, and offer a paged way to find and
repair affected items. Search is a read-side convenience; a projected empty
result is not used to authorize the removal or prove completeness. An accepted
draft edit, its effective membership at a given cut, and a complete published
catalog are distinct states. An ineffective point of focus retains its audit
history but is not a member of the published edition and is not mappable there.
Do not silently report an accepted but ineffective item as an included item.

Each new point of focus binds an exact durable parent-creation fact and verifies
that it names a criterion in the same edition and category. Its own acceptance
does not establish that the parent will still be included at a later publication
cut; effectiveness uses both exact states at that same cut. An edit naming a
known, valid but removed parent may be recorded in the draft, and is explicitly
shown as ineffective and needing repair. The command result says the edit was
recorded; it does not say the point of focus is included. An unresolved parent
fact remains pending, not proof of either existence or absence.

## Nonempty witness and publication race

Publication checks one exact included criterion incarnation as a nonempty
witness. To prevent that single witness from being removed between the check and
publication, one candidate has the selected criterion owner accept a **pin**
for the exact edition, draft generation, incarnation and publication attempt.
Pin and remove/replace are decisions on that same criterion stream: if removal
wins, pin is refused and the publisher chooses another witness; if pin wins,
removal cannot take effect while that pin is held. This is a bounded decision
about one participant, not an edition-owned child roster. Other entries do not
have to be scanned or individually settled for the publication write.

Only a publication attempt authorized by the edition owner may request the pin.
The edition stream uses its own expected version to order that attempt against
publication, abandonment, label transition and any competing attempt. Publication
binds the exact edition, draft generation, attempt identity, witness identity,
witness incarnation and accepted pin token. A timeout or missing reply is not
an abort. The owner must reconcile that exact witness and attempt; it must not
assume the pin failed and choose another witness while the first result is
unknown. The accepted pin itself is durable and the exact target outcome is
recoverable after a crash.

Edition publication and abort compete by expected position. An OCC conflict
reloads the same attempt and returns or reconciles the winning terminal fact; a
stale publisher must not simply use the newer edition revision to append its
old publication. Unknown terminal outcomes remain pending. When abort wins, the
edition's committed abort fences that exact attempt from later publication. Its
durable reactor issues an idempotent, token-specific abort fence to the **one**
selected witness, not to the edition's entries. The target verifies the exact
committed abort and durably fences that token even when no pin is currently
present. If the pin arrived first, the fence releases it; if the fence arrived
first, a delayed first pin is refused. A stale pin handler that read before the
fence cannot append after it at the old expected stream version. A repeated pin
or fence for that token returns its original attributable outcome and the
current aborted attempt status rather than reacquiring protection. A previously
accepted pin remains a historical accepted fact even though its attempt is now
aborted; a never-accepted late pin is refused. Changed intent under the same token conflicts. The abort
itself may be recorded while this one-witness cleanup remains explicitly pending;
it is not reported as safe for witness reuse until the target fence is confirmed.

If publication wins, the witness remains pinned until the target verifies the
exact committed publication and converts that pin into a **permanent structural
publication fence** for this incarnation. The fence does not open a gap: a lost
or delayed follow-up leaves the original pin in force. An exact retry after the
fence returns the recorded outcome; a delayed removal or replacement must not
be accepted. Every other structural writer also needs the qualified shared-source
publication cut, or an equivalent authority, so that a post-publication append
cannot be reported as an accepted edit. Per-entry source classification remains
pending until proven; the permanent witness fence alone does not establish it.
The publication and abort reactions and their exact participant outcomes must be
durably retried and reconciled after crash or response loss. They address one
witness for one attempt, with no child fan-out.

The accepted pin must order against all earlier operations on that same criterion,
not merely against already-materialized outcomes. If an earlier local proposal is
ambiguous or not decided, the owner must resolve or durably fence it before
claiming the witness is included. A stale pre-pin read followed by an append is
not sufficient. Every relevant writer, including import and repair, must use the
same participant authority and expected-stream-version rule.

## Exact cut and recovery

The publication event is the candidate marker; its shared-scope source position
is discovered after commit. The edition's immutable set is defined from one
complete, retained, authoritative cut `C`: each criterion and point of focus
intent is interpreted as of `C`, and each effective point of focus's exact parent
is interpreted at **the same** `C`. A parent removal after `C` cannot mutate the
published set. A physical append is not automatically a member: late or stale
operations after `C` need a durable refused/nonmember result, not a misleading
success.

An intent recorded before `C` can have its terminal participant outcome recorded
after `C`. Reading outcome events only through `C` would omit that answer. A
catalog therefore discovers all relevant pre-`C` intents and reconciles each to
its exact durable accepted or refused outcome, even if the outcome is later; the
outcome binds the original intent and decides it deterministically according to
the qualified protocol. It excludes intents after `C`, not later outcomes of
eligible pre-`C` intents. Until that coverage and reconciliation are established,
the catalog and downstream readiness report incomplete/pending rather than empty
or complete. This is read-side materialization, not a publication write gate.
Readers distinguish accepted, refused and unknown per participant.

The write decision does not enumerate other entries. Paged or streamed reads
may materialize the version after publication, and an exact single-entry lookup
may join that entry with its one parent. Stable pagination, exact as-of state,
the index or workspace used for this join, and coverage through `C` need real
implementation evidence. A hash, count, unverified projection or bare marker
does not establish completeness. A complete view is not a pretext to read the
whole catalog into the publication aggregate.

The design still needs a deterministic outcome for **every individual entry**:
competing edits for the same `(edition, identifier)` must be ordered by the entry
owner; late or ambiguous retries must reconcile their exact command identity and
intent. If two staged proposals can commit before `C`, a later worker winning
an outcome append does not necessarily select the earlier proposal. Prove
per-identity source-order selection or prohibit competing accepted proposals on
that entry; define the behavior for every timeout and refusal. Time-sensitive
permission, parent validity and edition-admission checks require authoritative
ordering at their specified business instant; a later worker's current read
cannot retroactively decide historical membership. Program creation may proceed
once the edition is validly published while unrelated participant outcomes are
pending; it must not convert an unknown participant into an absent one.

Local Fitz source describes ordered area/realm Stream pattern reads, but deployed
broker version, routing, durable retained history, contiguous reads, scope
generation, exact resource-to-scope mapping, as-of lookup, crash/unknown-commit
handling and target-side fences are not established by this design. Portia's
exact-resource reader exposes a resource offset rather than the shared-scope
position. Do not compare it with a scope marker as though they were the same
cursor. A usable implementation needs a qualified common-order path or a real
equivalent atomic authority; an event-store name alone is not that proof.

## Required cases

| Sequence | Expected result |
| --- | --- |
| `P1` and `F → P1` exist; `P1` is removed before `C` | `F` remains a recorded draft entry but is ineffective and excluded from the published cut. No child write is needed. |
| `P1` is removed; `P2` is created under the same identifier | `F → P1` remains ineffective. A separate user-attributed revision can bind `F` to exact `P2`. |
| Parent category is corrected through a new incarnation | Old references remain ineffective; a repaired point of focus must match the new parent's category. |
| `P1` and `F → P1` exist before `C`; `P1` is removed and re-added as `P2` after `C` | Published `F → P1` remains effective: both `F` and its exact parent incarnation are resolved at the same `C`. Late edit intents receive their defined nonmember/refusal outcomes. |
| A pre-`C` entry intent settles after `C` | Materialization follows that exact intent to its durable outcome, even though the outcome event is later than `C`; until then the view is pending, not absent. |
| A paged reader has not established coverage through `C` | Its cursor remains bound to the exact generation and cut; an incomplete view cannot prove that an entry is absent. |
| Removal wins the witness-owner append race | Pin refuses; publication chooses a different included witness or stays pending. |
| Pin wins the witness-owner append race | The exact witness cannot be removed before the matching publication or fenced abort. A stale expected-version append is rejected. |
| Abort commits, its target fence arrives before a delayed first pin | The target durably fences the token even if no pin exists. The delayed pin is refused; an old read cannot append at the pre-fence version. |
| Pin commits before abort's target fence, or its response is lost | The witness remains protected until the exact committed abort is reconciled and the target fence is accepted. Do not select another witness on the assumption that the first pin failed. |
| A duplicate pin arrives after an aborted attempt's fence | It returns the original terminal outcome and never reacquires the pin; changed intent conflicts. |
| Publication commits and a removal is delayed | The existing pin stays effective until converted to a permanent structural publication fence. The removal cannot be accepted; a retry of the pin or seal does not open a gap. |
| Edition OCC conflict occurs while publishing | Reload the same attempt and reconcile the committed winner; never append that stale attempt using the winner's newer revision. |
| Publisher times out after sending publication | Outcome remains unknown; reconcile exact edition and attempt. Do not release the witness pin on timeout. |
| The publication sees one witness while another point of focus has an unknown parent | The witness proves only that at least one criterion exists. The catalog/that participant remains incomplete until its own authority and source coverage are resolved. |
| Two incompatible edits of the same entry race | Decide one deterministic per-entry order and exact outcomes; a later worker's arbitrary scheduling cannot define the published set. |

## Jev review and qualification

The exact synthetic [request](criteria-draft-lifecycle-jev-request.json) and
[response](criteria-draft-lifecycle-jev-response.json) use `jev-1.14.0`. The
model rated an old point of focus effective after parent removal 0.06, removal
requiring a child fan-out 0.10, re-add automatically reviving the old reference
0.04, the stated pin protecting the witness 0.95, safe release on timeout 0.02,
and an unverified provider satisfying the assumptions 0.02. It rated the
user-visible behavior difference from refusal-on-removal 0.94. These are answers
to stipulated traces, not evidence that the protocol is implemented or that
runtime ordering, admission, or recovery is correct.

A second [request](criteria-draft-witness-fence-jev-request.json) and
[response](criteria-draft-witness-fence-jev-response.json) probes delayed pins,
abort and publication fencing. Jev rated the delayed first pin accepted after a
durable abort fence 0.05, an aborted duplicate reacquiring a pin 0.06, and a
stale publisher safely retrying at a newer edition revision without checking the
abort 0.06. It gave only 0.68 to the stated permanent-fence protection and 0.47
to the counterexample that releases a successful pin without a replacement
fence; those answers are inconclusive. The counterexample and source-level
expected-version reasoning remain actionable regardless of the model response.
These scenarios assume the specified target-owned behavior and do not prove it
exists or survives a crash in the configured provider.

Before treating the technical design as qualified, establish the actual
source-order and as-of contracts; exact participant and witness-pin state
machines; accepted/refused/pending outcomes and deterministic per-entry replay;
publication versus abandonment and concurrent edits; permission timing; and
crash/retry/fence behavior. Verify those conditions against the deployed
provider and executable concurrent, replay and unknown-commit tests in the
implementation phase. This is a concrete design direction with open authority
proof, not a claim that the entire feature is ready to implement unchanged.
