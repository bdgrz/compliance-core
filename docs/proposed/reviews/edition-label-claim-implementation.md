# Edition label claim implementation review

Status: **source and focused tests authored; execution and integration pending**.
No compilation or tests were run in this round, per the feature-wide instruction.

- **Identity:** one normalized platform edition label, addressed by a typed
  deterministic identity in the `bdgrz/criteria-edition-labels` stream area.
- **Invariant:** at most one edition operation holds a label at a time. A new
  holder's generation is exactly the preceding generation plus one; only the
  exact holder, operation and generation can confirm or release it.
- **Lifecycle:** an exact reservation retry returns the previously recorded
  reserved, confirmed or released status without reacquiring the label. Changed
  request evidence, generation or actor under the same edition operation is a
  conflict. Rename release requires a separately identified destination-claim
  confirmation. Released generations cannot clear a newer holder.
- **Boundary:** the aggregate keeps the low-churn history of this one label's own
  decisions. It does not store edition entries. Its event-ID parameters are
  references, not proof that the other stream exists. A trusted coordinator must
  load and verify the exact committed edition and replacement-claim facts before
  calling confirmation or release. A competing reservation is a no-fact refusal;
  the edition must record its own source-backed terminal outcome, and every late
  reservation must trigger reconciliation against that edition's terminal fact.
- **Authored tests:** `EditionLabelClaimTests` covers normalization, invalid input,
  no-fact refusal, exact versus changed-intent retry, confirmation, generation
  fencing, release and reassignment, valid replay, and competing expected-position
  appends. These are authored tests, not executed evidence.

The focused [Jev request](edition-label-claim-impl-jev-request.json) and
[response](edition-label-claim-impl-jev-response.json) evaluated the aggregate
source with `jev-1.14.0`. For the supplied scenarios Jev returned 0.11 for an old
reservation retry reacquiring a released claim, 0.08 for its old release clearing
a newer holder, 0.04 that this aggregate itself verifies the referenced edition
fact, and 0.02 that timeout alone permits release. The generation-order answer
(0.21) was not decisive; the code explicitly requires the next generation, and the
authored tests still need execution. Jev does not establish source authority,
runtime behavior, provider concurrency or serializer integration.

Remaining integration: register the three new event contracts in shared event
and JSON tooling, wire source-verified edition/claim coordination and late-message
recovery, and run the focused and full feature gates after assembly.
# Selected reservation and release-source follow-up

The claim retains its current holder and only one explicitly selected historical
reservation. Reserve, confirm and release require selecting the edition and
operation before hydration; read-only owners may omit that selection. Replay of
an old selected reservation does not clear a newer holder. This bounds retained
reservation state, but Portia still loads the complete claim stream; no fixed
lifetime bound on that stream is claimed. An unselected replay is not proof of
historical operation-ID uniqueness.

The unregistered release handler requires exact committed reservation, original
intent and terminal edition facts. For a completed rename it also checks the
replacement reservation, intent, edition decision, confirmation and generation.
Cancellation and refusal can authorize cleanup of a reservation delivered late.
Missing source facts remain pending; an ID without the matching fact is not proof.
Durable per-operation delivery and recovery for both ordering directions remain
open. In particular, do not register a shared checkpoint reactor that waits
indefinitely on one contested reservation.

The exact [Jev request](criteria-label-selected-jev-request.json) and
[response](criteria-label-selected-jev-response.json) used `jev-1.14.0`.
Probabilities of yes were 0.06 for retaining all historical reservations,
0.06 for selected replay avoiding Portia's full stream load, 0.12 for safely
mutating without selection, 0.13 for an exact old release removing a later
holder, 0.05 for accepting a changed request as an exact retry, 0.81 for delayed
reservation cleanup after cancellation, 0.08 for a different cancellation
authorizing release, 0.08 for releasing without replacement confirmation,
0.16 for releasing without replacement reservation, and 0.07 for generic
source scans proving freshness after an ambiguous commit. The model returned
0.29 that a shared checkpoint could advance while it synchronously retries one
claim forever; source-level checkpoint behavior and executable tests, not that
number, determine the answer. These are review signals, not proof. Focused
tests were authored and were not run as requested.

