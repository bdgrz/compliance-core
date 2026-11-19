# Criteria edition values and disclosures

Status: **partial**, 2026-11-05. Domain owners and focused tests are authored;
they have not been compiled or run. The owners are not exposed by a registered
write process. This implementation supports one `coverage_note`, `source_url`, `content_rights`
or `numbered_criteria_complete` field per owner, or one disclosure identified
by `(edition, category, code)` per owner. A separate immutable operation owner
binds one target-scoped operation to its exact intent and actor. Neither owner
keeps a list of other targets or operations. The value owner holds one current
value, a revision and at most one pending proposal; it selects one operation's
history for retry receipts during replay.

Proposals refer to the exact draft-request and operation-claim source event IDs,
require server-derived operator admission and reject seed edition requests. An
accepted or refused outcome names one exact pending proposal and its decision
basis: a qualified observed draft head, publication, or abandonment. Evidence
records that edition event's identity and source positions and the proposal's
area position. These references are not source proof; independent readers must
re-read the exact events and establish edition coverage through the committed
outcome, including any intervening terminal cut. Only the caller can prove the supplied request
and claim are committed, source ordering,
current operator authority, the active draft, and the publication or abandonment
cut. The aggregate does not itself prove those facts. There is no target attempt
fence or source-currentness provider in this slice: lost or ambiguous writes
must remain pending, including attempts to advance execution generation, until
qualified recovery exists. A field or disclosure absent from a projection is
unknown, not a complete or supported claim. No clear or withdrawal operation
is exposed; no field or disclosure is a publication prerequisite.

Content is bounded independently: coverage and disclosure notes are at most
2,000 UTF-8 bytes, URLs 1,024 bytes, disclosure codes 128 bytes, and each
serialized business event including admission evidence at most 8 KiB. These
are per-record limits, not a count limit. Operator commands, a source-checked
proposal path and one independently retried decision process per proposal are
authored but unregistered. Qualified source providers, cut-aware projection,
retention and qualified paginated completeness remain integration work. A target owner is expected to be low churn; Portia
still loads its full stream. The source does not establish an unbounded-history
or generation-rollover guarantee, so that is a future qualification question.

## Jev review

The exact owner, event, model and rule source is preserved in the
[request](criteria-edition-value-jev-request.json); the
[response](criteria-edition-value-jev-response.json) records the live
`jev-1.14.0` answers. Jev assigned 0.96 to separate target identities, 0.07 to
an edition roster, 0.07 to changed intent succeeding as an exact claim retry,
0.04 to the aggregate proving cross-stream authority itself, 0.15 to one
pending item bounding a fully loaded unrestricted stream, 0.94 to rejecting a
different proposal ID, 0.93 to checking payload bounds, 0.07 to a numbered
criteria declaration proving full framework support, 0.24 to the source
containing a durable target-attempt fence, and 0.92 to rejecting another
framework. Jev assessed direct untrusted admission as potentially accepted at
0.51; source inspection confirms the evidence is shape-bound, not independently
source-authorized, so it must never be exposed from an untrusted caller.
Jev assigned 0.90 to requiring coverage through the committed outcome, 0.18 to
accepting a proposal after an intervening publication, 0.67 to classifying
intervening abandonment as abandoned, and 0.21 to accepting a draft decision
whose named head is a publication-started state. The target aggregate does not itself
verify its references against committed source. The source race cases still
require executable tests and provider qualification.

The independently checked label participant chain now requires qualified
cross-area causality, rather than comparing unrelated area offsets, and qualified
same-resource continuity through label confirmations and refusals. A missing
causal proof or witness fence remains pending. Jev assigned 0.17 to comparing
unrelated area offsets directly, 0.04 to accepting absent causal proof, and 0.18
to verifying cleanup without the referenced witness fence. No production causal
order provider has been qualified.
Probabilities are advisory and do not establish authority or storage guarantees.
