# Criteria and program creation: selected decisions

Status: **product decisions selected; implementation qualification required**,
2026-11-05. The product owner chose to follow the recommendations and asked for
Jev review. The source of truth is the updated [criteria](../contexts/criteria.md)
and [program](../contexts/program-and-scope.md) contexts; this is the evidence
record for the cross-cutting choices.

1. **Editable drafts:** entries have independent incarnations. Removing a parent
   criterion makes points of focus referring to that incarnation ineffective;
   re-adding it does not automatically revive them. Operators can repair each
   affected point within the same draft. The removal itself does not fan out.
2. **Publication:** the edition uses one pinned criterion as a nonempty witness.
   Its committed `CriteriaEditionPublished` event is the sole structural cut.
   Publication does not enumerate children or wait for all individual entry
   outcomes. A pre-cut operation may settle later; exact consumers wait for its
   outcome and source-order proof.
3. **Program creation:** a client chooses an authoritatively published edition
   and supplies a stable program ID. Unrelated pending criterion outcomes do not
   prevent creation. Incomplete source coverage remains visible; entry-dependent
   operations must establish their own exact accepted evidence.
4. **Edition labels:** each normalized label has one owner. Draft intent comes
   first; if the initial label is unavailable, choose another using a new
   operation ID without replacing the edition ID. A rename keeps its old claim
   until the new claim and exact edition change are confirmed.
5. **Operator admission:** a successful, current, coherent source-authoritative
   decision covers the exact actor's operator grant, identity/session authority,
   current policy generation and applicable vetoes. A subsequent revocation
   stops new admissions but does not cancel that exact admitted operation.
   Current authorization is still required to disclose a retry result.
6. **Program-create admission:** one fresh, coherent authority decision covers
   an active tenant, eligible member episode, and one valid organization-wide
   permission path. A later revocation does not cancel that uninterrupted
   admitted attempt. `ProgramCreated` remains the sole durable creation fact;
   if a prior append may still arrive, a same-stream fence or definitive provider
   settlement precedes a fresh authorized attempt. A fence binds the recovering
   request, not an unknown lost intent.

The exact [Jev request](criteria-product-recommendations-jev-request.json) and
[response](criteria-product-recommendations-jev-response.json) evaluate these
stipulated examples. Jev returned 0.04 that an old point of focus becomes
effective after a different parent incarnation is added, 0.12 that removal
requires writing each child, 0.03 that a pending criterion proves absence, and
0.04 that pending unrelated criteria alone prevent selecting an already validly
published edition. It returned 0.04 that readiness may claim complete coverage
while a required outcome is pending, 0.06 that a stale pre-removal read proves
the witness exists at publication, and 0.03 that a missing publication reply
proves a witness pin is safe to release. These are model judgments on supplied
premises, not proof that the implementation or provider satisfies them.

A second [request](criteria-final-protocol-jev-request.json) and
[response](criteria-final-protocol-jev-response.json) challenged the final race
examples. Jev returned 0.08 for an old label generation releasing a new holder,
0.06 for delayed confirmation activating a cancelled draft, 0.93 for a refused
pre-cut replacement leaving its previous incarnation effective, 0.95 for an
admitted program creation completing after later revocation under the selected
policy, and 0.03 for an unqualified Boolean operator check proving current source
authority. The delayed-pin answer (0.30) and the missing-page trace (0.73) were
inconclusive model signals; the described counterexamples and implementation
tests remain necessary. None establishes provider guarantees.

The detailed reviews retain further Jev answers and adversarial traces:
[draft lifecycle](criteria-draft-lifecycle-design.md),
[publication protocol](criteria-publication-protocol-design.md),
[label claim](criteria-label-claim-design.md),
[operator authorization](criteria-authorization-design.md) and
[program creation](program-creation-criteria-design.md).

The selected decisions are not a declaration of implementation readiness. The
actual current-read authority, per-entry and witness fencing, scope routing,
ordered and retained source, exact as-of index, stable pagination, unknown-commit
recovery, migration and native deployment behavior still need source-backed
implementation and focused adversarial qualification. Existing coverage and
legacy data must not be silently upgraded or guessed.
