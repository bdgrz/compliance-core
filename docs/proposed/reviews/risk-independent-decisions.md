# Risk and provider oversight: independent decision candidates

Status: **candidate; product semantics and authority unqualified**, 2026-11-05.
The [context](../contexts/risk-and-provider-oversight.md) withdraws its prior
all-selected-risk triage and all-report-item review completion gates. Neither
streaming a cut nor splitting admissions into many streams makes the decision
local. Accepted product decisions remain in force until explicitly changed.

## Reassessment requirements and triage

One requirement is identified by one risk and one source-reason episode. Its
own stream records that one requirement, so a widely used risk does not replay
one event per related trigger or own a trigger roster. A repeated exact operation
is a no-op; changed intent with that identity is refused. Each requirement needs
source-bound authority, including the rule for a risk retiring while the
requirement is being recorded. The transition and recovery are not established
by a prior risk read or by deterministic IDs.

One candidate records the lead's attributed triage judgment, with an explicit
reason if the lead judges no risks affected. Every selection and its requirement
outcome is independent and visible; a selected requirement may be pending,
accepted or refused after the judgment. A recorded judgment does **not** imply
the requirement was accepted, and a lagging list cannot prove none was selected.
This differs from the previous candidate's promise that every selected target
had a terminal outcome before triage completed. RP-05 requires selection of
affected risks or a reason for none; whether the product also requires an atomic
all-target completion assertion needs an explicit decision. The authority of
late selections and mutually exclusive `none` judgments also needs a protocol.

A new requirement can race a residual assessment. Naming the latest observed
requirement does not prove it was the latest at assessment commit. A historical
assessment of exact versions plus separately visible later requirements is
possible, but weakens a current-at-commit assertion; it is not approved as an
equivalent. Exact source admission and handling of risk retirement, changes in
treatment and appetite, pending and ambiguous outcomes remain open.

## Provider conclusions and report items

M0-D11 requires specified assurance fields and a provider review. The exact
source artifact and revision can be the subject of a person's attributed
conclusion; each CUEC, exception, service or gap can have an independently owned
item and result. This alone does not prove the source was exhaustively extracted
or that every relevant item was reviewed. If a product rule demands mechanically
complete extraction or a conclusion guarded by every item outcome, the candidate
does not satisfy it; that requirement needs an explicit decision and a compatible
domain design. A user's professional attestation is evidence of their judgment,
not a technical completeness proof.

For `acceptable_with_gaps`, one exact, immutable, admitted gap result is a
possible fixed-size witness that a gap exists. It does not establish that all
gaps were identified. Its acceptance and its relationship to the exact report,
provider lifecycle, authorization and concurrent corrections must be qualified.
The provider's risk acceptance and each coverage gap keep independent lifecycles;
linking an acceptance does not close a gap.

The duplicate-provider-name guard is advisory under RP-09; the context's command
table was corrected so it no longer promises a cross-stream uniqueness guard.

## Jev review

The exact [request](risk-independent-decisions-jev-request.json) and
[response](risk-independent-decisions-jev-response.json) retain the live
`jev-1.14.0` answers on synthetic, explicit scenarios. Jev rated: triage judgment
already proving a delayed target accepted 0.05; submission while a target remains
pending differing from all-settled completion 0.96; source-item extraction proven
by a conclusion 0.04; one immutable gap proving at least one 0.91 and all gaps
0.03; a prior latest-requirement read proving commit-time currentness 0.31; an
explicitly historical claim avoiding a present-currentness assertion 0.89; and
projected zero proving all items were included 0.04.

The latest-requirement answer is inconclusive as a model signal; the concrete
concurrent-write counterexample controls the design assessment. Scores are
advisory and do not prove provider ordering, data retention, authorization,
replay, product meaning, or implementation correctness. No code, build or tests
were run for this documentation change.
