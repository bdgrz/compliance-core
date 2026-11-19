# Methodology release boundary: ordered-view candidate

Status: **candidate; unqualified**, 2026-11-05. This review explores an immutable
whole-version *read* without a write decision that loads, enumerates or settles
all template entries. It does not approve changes to the existing product rules
or claim the provider already supplies the necessary end-to-end protocol.

## Core distinction

Each entry decides its own revision and binds it to an exact draft and editing
epoch. A freeze marker, committed into the same durably ordered scope, can
identify that draft's reviewed cut. The release binds that exact approved cut
and its pinned, accepted base release. Each key is resolved from that draft's
accepted changes across **all** its editing epochs through the cut or, when
unchanged, from the base. Discarded or unrelated drafts never leak into the
release merely because their facts precede the marker. A returned review alone
does not discard the draft. The marker is a bounded fact, not a roster or child
ledger. A read model can later enumerate the retained as-of state incrementally.
Publishing does not check every entry; consumers must not mistake a partially
caught-up index for a complete release.

The marker must be accepted under a specified lifecycle and race protocol.
Source ordering cannot turn an unaccepted proposal into a successful entry
revision. Unknown writes, release requests and participant decisions remain
pending until their actual outcomes are determined. A refused release does not
create a published version. Review and publication must bind the exact same
freeze; later editing epochs cannot silently enter a reviewed release. Define
how a request's marker relates to its later acceptance and to intervening edits
before implementing this candidate. The earlier contract resolves or fences all
edits admitted before freeze; excluding an undecided pre-freeze request is a
behavior change unless it was not admitted under an explicit, justified rule.

This may preserve immutable release membership, but it does **not** by itself
preserve whole-draft reviewer independence, atomic whole-version application,
or the rule that the next version cannot start until the preceding application
is complete. Each needs a distinct decision and proof. In particular, a
per-entry review lets a person review entry B after authoring entry A, which the
old whole-draft rule forbids. Allowing next-version entry A2 while the previous
version's B1 remains pending also changes behavior. These are product choices,
not facts the model can decide.

## Authority and failure traces

- A reviewer's absence from a lagging author index does not establish that they
  authored no accepted revision. An actor-specific exact-cut lookup could avoid
  loading all entries, but its event provenance, cross-owner races, full-history
  coverage and checkpoint must be demonstrably authoritative. A pre-written
  author claim whose edit is later refused produces a false positive; an
  asynchronously written claim after an accepted edit can produce a false
  negative. The source acceptance must be the fact indexed.
- An index whose durable checkpoint advances before its record updates can
  recover after a crash with missing entries despite claiming the marker was
  processed. Each shard or partition, source generation, retained history and
  checkpoint-to-data write boundary must be accounted for. A system that cannot
  prove coverage displays incomplete or pending.
- Each target accepts or refuses one entry application. A timeout is unknown,
  exact retry reuses the operation identity, changed intent cannot reuse it, and
  a refusal must fence an otherwise delayed request. An application for another
  key does not establish this key's outcome. A same-key ordering rule needs a
  real conditional owner or equivalent; two preliminary reads do not suffice.
- A release's complete application status is meaningful only over the exact
  release membership and matching terminal outcomes. A report can evaluate this
  as a read subject to explicit source coverage and freshness, but a stale or
  partial listing cannot certify it. `refused` is terminal yet does not mean
  `applied`. If the next-release guard is retained, an advisory dashboard value
  cannot authorize the subsequent write.
- Resolving and paging an as-of version may scan or index retained source data.
  That cost is real, but does not require collecting a dataset in one aggregate
  or sorting it in application memory. Stable keyset pagination must remain
  tied to the exact approved cut across index rebuilds and later edits. Base
  release overlays, replay, retention and compaction require measured bounds or
  costs, and indexes need proof. An index design that distributes a
  per-child admission ledger among coordinator aggregates would not satisfy the
  boundary direction merely because each aggregate is small.

The local Fitz checkout `d8786e74d10ccfe68a0d68ab7ba562da453904a3`
documents commit-time area/realm offsets and warns that `DROP` can remove
history or reset a scope. Local Portia checkout
`7a6fbb6477a839bfe36cf22e57c069a671d207e2` verifies consecutive scope offsets
in its Fitz pattern reader. These are source observations, not proof that the
deployed versions, persistence setting, routing, retained source, replay index or
checkpoint transaction meet the candidate's requirements.

## Jev review

The exact synthetic questions and full `jev-1.14.0` response are retained in the
[request](methodology-ordered-release-jev-request.json) and
[response](methodology-ordered-release-jev-response.json). Probabilities below
are the model's answer to the stated propositions, not correctness guarantees.

| Proposition | Yes probability |
| --- | ---: |
| A fully specified ordered snapshot requires the marker write to load every entry | 0.18 |
| A lagging projection proves a second pre-marker entry is absent | 0.07 |
| A proposal ordered before the marker is accepted despite participant refusal | 0.05 |
| A post-marker proposal can be acknowledged as part of the published version | 0.03 |
| A lagging author index assures reviewer independence | 0.35 |
| A complete, proven exact-cut actor lookup must load all entries | 0.14 |
| Two same-key applications without common authority can race | 0.86 |
| Reporting all entries applied when one is pending overclaims | 0.97 |
| A claim written before a refused or unsent edit proves accepted authorship | 0.05 |
| A checkpoint advanced before a lost index write proves completeness | 0.07 |

The 0.35 stale-author answer is inconclusive despite the explicit missing fact;
it does not refute the race. The favorable exact-cut and ordered-snapshot answers
assume completeness and ordering instead of proving them. The snapshot question
also omits draft and epoch isolation; independent review exposed that a simple
latest-before-marker rule would include a discarded draft. The corrected
draft-and-base overlay above is not established by the model answer. Validate
the actual source, durable outcomes, retries and concurrent behavior before any
lock or implementation. No builds or tests were run for this design review.

The subsequent [draft-isolation request](methodology-draft-isolation-jev-request.json)
and [response](methodology-draft-isolation-jev-response.json) test the corrected
draft, epoch and base cases. Jev rated the discarded-draft leak at 0.74 and the
missing-earlier-epoch error at 0.61; neither score is a decisive signal, but
both mistakes follow directly from the stated traces. It rated incorrectly
including an edit after the approved cut as reviewed at 0.08, preserving the old
admission guarantee by silently excluding unresolved earlier requests at 0.12,
and missing an author from an earlier epoch at 0.91. These are adversarial
examples, not evidence that the provider or runtime implements the candidate.
