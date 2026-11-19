# Criteria entry decision source proof

Status: **implementation candidate; provider qualification and execution tests remain open**.
Each structural proposal is independently scheduled and refers to the exact
operation claim and draft. A draft decision records the observed edition head
and proposal positions. Its writer requires a qualified current edition head and
an expected-position target write. No configured provider currently grants that
head guarantee.

An independent verifier re-reads the exact proposal, decision, and named edition
head, then requires contiguous edition-source coverage through the committed
decision's area position. If publication preceded the decision, the proposal
must precede the publication cut. An abandonment that precedes a delayed
draft-based decision is reported as abandoned, not effective published state.
A named old head, empty page, or event ID by itself does not establish the
required currentness or coverage. Claim, parent-incarnation, and publication
authority remain separate source checks.

The exact Jev [request](criteria-entry-decision-jev-request.json) and
[response](criteria-entry-decision-jev-response.json) preserve the inputs and
results from model `jev-1.14.0`:

| Question | Yes probability |
| --- | ---: |
| Can a valid proposal before publication contribute when its decision follows the cut? | 0.80 |
| Does an old draft head alone prove it was current? | 0.05 |
| Does a quiet source without qualified coverage establish no earlier terminal? | 0.08 |
| Can an abandonment race be represented as a published edition? | 0.05 |
| Does a claim ID alone prove a matching earlier committed claim? | 0.07 |
| Can qualified edition-source coverage through the decision establish the relevant ordering? | 0.77 |
| Does this design itself prove a production implementation? | 0.03 |

Jev was uncertain whether the deliberately post-cut proposal was excluded
(0.35 yes). That is not evidence to admit it: the decision rule compares source
positions explicitly, and a proposal at or after publication cannot contribute.
The model results do not prove any storage, replay, source-retention or race
guarantees. The current provider and end-to-end behavior still need qualification.
