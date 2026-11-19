# Readiness bounded-evidence review

2026-11-05. Scope: the readiness context's assessment, gap plans and Type I
entry decision. The proposed decomposition is not an implementation approval.
The original product decisions RD-01–RD-06 remain in force; RD-07 remains
unlocked pending the qualifications below.

## Local-decision refinement

The prior version replaced large input/gap events with individually owned facts,
but still required a coordinator to seal an assessment or entry decision only
when every item had settled. That remained an all-child write decision, even if
its records were small or the coordinator consumed a projected certificate.
It is withdrawn. A per-run window, count, hash, renamed attestation or
segmented outcome chain does not resolve that dependency.

The candidate keeps a small assessment **request** and independent
rule/subject/result facts; the readiness verdict and lists of gaps and plans
are read-side calculations. A result is local to one rule/subject/discriminator
and should depend on only the few exact facts its rule requires. A rule that
itself needs an unbounded child set for a decision still needs redesign. The
read may traverse many results, using a paged/streamed traversal and constant
working state for the verdict fold; no assumption is made that source replay,
rule-specific joins, elapsed time or index size is constant. A missing or
unproven required subject makes coverage unknown/incomplete, not ready. A
lagging zero-row projection or matching counts cannot establish completeness.

**This is not yet equivalent to the accepted entry rule.** RD-02 requires an
Org Admin's decision to cite an exact complete assessment, and M0-D23 requires
an immutable reproducible calculation. If the write must establish the complete
result of all independent items, an external service's summary merely moves
the all-child guard. One explicit alternative allows the manager to decide while
the evaluation is incomplete or coverage unknown and surfaces that distinction
in reads. That would change RD-02 and possibly M0-D23, and has not been
approved. Until a product decision or a genuinely local equivalent is found, do
not wire the candidate as authority for a Program stage transition.

A completed historical assessment remains eligible under RD-02 even after new
source facts arise. But reproducing its exact required subject universe, source
versions and per-rule results needs an actually qualified immutable source cut
and retention. A pointer, content hash or event timestamp proves neither a
complete cut nor a common instant. Its read view must distinguish historical
assessment time, its current projection coverage, and current source state.
Late, reordered or ambiguous result writes cannot mutate accepted historical
evidence; protocol, ownership and actual provider guarantees remain open.

RD-01 allows entry with gaps and says they are listed with plans. Listing is a
read concern, but reconstructing the plan each gap had at the decision instant
requires a defined source frontier and retained plan versions. Current plans or
independently sampled plan revisions need a different label. Whether a missing
plan blocks entry is also unspecified. Do not silently adopt a weaker temporal
interpretation or a new missing-plan policy. `not_yet` evidence requirements
remain open.

One rule can produce more than one gap per subject. A `(rule, subject)` plan
key would conflate them; the proposed identity adds a rule-defined result
discriminator. Stable cross-assessment and cross-rule-version migration, and
any carry-over of historical plans, still need an explicit contract.

## Local-decision Jev probe

The exact synthetic [request](readiness-local-decisions-jev-request.json) and
[response](readiness-local-decisions-jev-response.json) use `jev-1.14.0`.
Values are yes probabilities for the stated questions, not architecture proof:

| Case | Probability | Interpretation |
| --- | ---: | --- |
| A write waiting for all results still depends on all results when another service computes the status | 0.90 | Supports withdrawing the former seal. |
| A read service can stream independently recorded results without placing them on a parent aggregate | 0.91 | A read-side option, not proof of complete source coverage. |
| A lagging projection with zero gaps proves no authoritative gaps | 0.04 | Zero is not completeness. |
| Missing projection coverage should remain unknown | 0.93 | Do not invent complete status. |
| Permitting an incomplete assessment weakens the existing complete-assessment guard | 0.73 | Semantic change remains explicit; model signal is not a product decision. |
| A newer source fact alone makes a completed historical assessment ineligible | 0.13 | Equivalent inverse of the 0.87 answer that it can remain eligible, as RD-02 permits. |
| Current plans necessarily show their values at last week's decision | 0.04 | Historical plan versions and timing matter. |
| Two gaps sharing rule and subject can collide if the plan key omits a discriminator | 0.85 | The source identity and migration rules still need qualification. |
| An assessment ID alone proves a complete immutable snapshot exists | 0.04 | Source evidence and completeness remain unresolved. |
| A known gap plus an unknown input can be `not_ready` under the stated unknown-is-incomplete policy | 0.21 | Consistent with incomplete taking precedence; use the actual rule, not the model, to decide. |

## Jev

Called TypeSafe System One `jev-1.14.0` on 2026-11-05; exact input and output:
[request](readiness-boundaries-jev-request.json),
[response](readiness-boundaries-jev-response.json). Values are probability of
"yes", not proofs:

| Question | Yes probability | Interpretation |
| --- | ---: | --- |
| Independent current reads establish one common decision instant | 0.04 | Consistent with the missing-cut finding. |
| Matching eventually consistent projection counts prove membership | 0.03 | Counts are not authority. |
| A missing required input precludes a complete ready conclusion | 0.92 | Unknown must remain explicit. |
| Independent bounded records can preserve completeness if the required authority and fences are actually proven | 0.91 | Supports the direction, conditional on the missing protocol. |
| Unfenced late work can invalidate a seal | 0.83 | A fence needs explicit design. |
| An empty projection proves no required subjects | 0.04 | Source authority must establish emptiness. |
| A historical complete assessment is inherently invalid solely because a newer one exists, given the stated policy | 0.13 | Consistent with RD-02; the approved policy remains authoritative. |
| The stated plan wording unambiguously resolves missing-plan policy | 0.07 | Treat policy as open. |
| Independently sampled plans necessarily reflect one decision instant | 0.05 | A decision cut needs authority. |
| Expected and observed counts alone prove required membership | 0.05 | Counts do not identify omissions. |

These results are bounded semantic review signals, not
proof that a distributed algorithm exists or is correct. Before locking this design, resolve the actual product meaning of a complete
assessment and how it can coexist with local write decisions, then qualify the
source and plan observation contract, admission, late work, recovery and
retention with source inspection and executable concurrency/failure tests.
