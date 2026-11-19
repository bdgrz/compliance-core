# System description and advisor collaboration boundary review

Status: **refinement in progress**. This review does not qualify an implementation
or change the accepted product decisions. The two context designs identify
required source guarantees that still need a durable, provider-supported
protocol and race/recovery evidence.

## Boundary findings and revisions

| Finding | Revision | Required proof before implementation |
| --- | --- | --- |
| Draft approval and version-line events duplicated a potentially growing citation collection; the nine DC 200 headings themselves are a fixed vocabulary. | A section carries authored text and a citation owns one exact source relationship. The former proposal to freeze, settle and scan all citations and authors at approval is withdrawn as an all-child write decision. | Decide whether per-section approvals and a derived description view can replace a single whole-description approval; they do not provide the same exact-set guarantee. Exact citation membership and source authority remain unresolved. |
| A previously committed significant assessment may not yet have produced its section review when examination close reads the current projection. | Projection absence is not proof. The former close-frontier proposal still needed all review-creation outcomes and is not an acceptable write-model solution. M0-D21 remains in force. | Reconcile the accepted no-open-review close rule with independent section reviews, including delayed assessments, reassessments, exact source versions and recovery. An eventually consistent derived status cannot by itself authorize close. |
| A handoff had all selected versions on its parent, and feedback accumulated replies. | One selection relationship per exact version; one immutable reply per feedback entry. The feedback owns its own current change-request resolution. One immutable designated anchor on the handoff is an optional, fixed witness of nonempty selection. | The anchor proposal changes selection workflow and is unapproved: removing it requires closing that handoff. Replacing the anchor repeatedly would grow the parent stream with selection turnover. Qualify source-version retention/invalidation, concurrent removal, close/membership ordering and exact tenant/program bindings. |
| Checking open handoff or membership before a feedback commit or content read leaves a close/revocation race. | Every successful access/feedback operation requires the relevant authoritative participant admissions ordered with close and revocation. | Define durable operation identity, retry and recovery, expiry, concurrency and transport behavior, including an in-flight response. Bytes already delivered cannot be recalled. |

The approved M0-D14 semantics remain: selected draft versions plus already
approved records are visible under the applicable access rules; closing or
revoking ends future access, while history remains. The conditions for approving
an exact system-description cut preserve all nine sections, correct citation
scope, and a reviewer independent of every draft author (unless an applicable
waiver exists). An independent-section design would change the meaning of a
single approved description and is not equivalent until that policy changes.
Predesignating the reviewer, with every admitted edit excluding that person
unless an applicable waiver is accepted, can enforce author separation locally
under those stated premises; it changes reviewer-selection timing and still
requires actual authoritative admission and freeze behavior. Resolution by a
later description version still requires that
the relevant section changed; otherwise the independent no-change path applies.
No automatic satisfaction of review, approval or separation-of-duties follows
from consultant feedback.

## Jev adversarial scenarios

Called TypeSafe System One `jev-latest`, resolved to `jev-1.14.0`, against
concrete races; see the [exact request](description-advisor-boundaries-jev-request.json)
and [response](description-advisor-boundaries-jev-response.json). Each value is
the model's probability that the question's proposition is true, not a proof.

| Proposition | Probability | Assessment |
| --- | ---: | --- |
| A parent approval event listing all citations can grow with their number | 0.96 | Matches the event payload directly. |
| A frozen cut that rejects new edits and settles earlier admissions prevents those edits appearing after the cut | 0.74 | Inconclusive model signal. Correctness depends on proving the stated fence and settlement premises in the actual provider and failure model. |
| A review projection can miss an already committed assessment whose reactor has not run | 0.96 | The asynchronous delivery ordering is sufficient to demonstrate the gap. |
| Feedback can be accepted after close if it relies only on a pre-close read | 0.75 | Inconclusive model signal. The stated interleaving establishes the time-of-check/time-of-use gap. |
| A reply can be independently immutable while the feedback owns its resolution | 0.96 | The reply identity and feedback identity give independent lifecycles. |
| A stale authors projection can allow an author to approve their own draft | 0.83 | A missing earlier revision directly violates the all-authors rule. |
| Closing a handoff can retract bytes already delivered | 0.02 | Closing can deny later decisions; a delivered payload is beyond revocation. |

These questions test the candidate boundaries and counterexamples. They do not
prove distributed atomicity, the size of live or replayed state, or implementation
correctness. Those claims require concrete authority protocols and targeted
concurrency, replay, crash and recovery tests; the affected designs remain
unqualified until then.

## Local-decision recheck

The follow-up [request](description-advisor-local-jev-request.json) and
[response](description-advisor-local-jev-response.json), model `jev-1.14.0`,
test designated reviewer, exact-version and anchor scenarios. Jev assigned
0.13 to a designated reviewer authoring an edit when every writer rejects that
author without a waiver, and 0.74 to a stale authors projection allowing a prior
author to be chosen; the latter is inconclusive although the interleaving is
direct. It assigned 0.80 to the conditional single-anchor nonempty property,
0.60 to an explicitly unfenced remove-before-open race, 0.01 to nine approved
section references proving that all citations were included and validated, and
0.05 to a changed current projection altering already-approved immutable exact
section revisions. The anchor result assumes that no independent operation can
remove or invalidate the anchor: that is a proof obligation, not a provider
feature established by the model. The 0.60 race answer is inconclusive; the
specified removal followed by an unfenced open can plainly yield an empty
handoff. The Jev anchor case assumed atomic replacement; the refined fixed-anchor
alternative avoids replacement churn, but has the stricter close-before-removal
workflow. Jev did not evaluate that workflow. These model values are advisory
and cannot validate implementation.
