# Policy campaign boundaries review

Status: **partial; launch and closure authority are not yet qualified**. This is
an invariant design review, not evidence that infrastructure provides the required
source, fence, replay, or participant coordination. No implementation is approved
by these answers alone.

## Authority and boundaries

The accepted [M0-D12](../decisions/m0-d12-policy-acknowledgement-training.md)
requires a frozen audience of workforce people, attributed later amendments,
separate person results, and preservation of results for leavers and movers.
A person may have multiple work relationships; removal requires the person to
stop qualifying, not merely one relationship to end. The campaign owns fixed
configuration and coarse lifecycle. Each campaign/person stream owns launch
origin, amendments, and that person's result. Lists and exports are projections.
No parent fact contains the audience, and no amendment is appended to a parent
stream. Enumeration and exports must be incremental; compact references cannot
hide an oversized manifest.

M0-D12 defines the core-security population using access to in-scope systems or
data. The prior PO-05 instead included every active employee and contractor.
That expansion is not established by a model score. M0-D12 also prohibits
inferring audience from application access, platform team membership, or the IdP.
How to interpret the qualifier and represent it in authoritative reconciled roster
observations remains unresolved; no access-governance source is prescribed.
Qualification or organizational scope can change without a relationship event,
so the required observations, triggers, amendment classification, temporal
semantics and gapless handoff for those changes must also be established.

The source model currently describes relationships and roster observations but
has not established a complete, immutable, replayable launch cut, a gapless
handoff, historical department values, or a rule for late and backdated
corrections. A deterministic person identity or an eventually consistent roster
cannot supply a missing person. Launch must remain blocked until a source
contract and temporal semantics are qualified, including overlapping
relationships and on-leave people.

M0-D12's role-targeted version also requires a nonempty list of roster teams.
That is an unbounded parent payload if stored directly in a policy event. A
candidate gives each `(version, roster department)` its own focused target and
uses one exact accepted target as a nonempty witness. This is not yet a complete
design: the witness must remain included when approval commits, all target
changes must bind to the correct immutable version, the approved membership must
be recoverable, and target edits must invalidate or precede the appropriate
review. An eventually consistent list, a hash or a chain containing one parent
record per target does not supply the missing authority. Whole-proposal reviewer
independence remains PO-01's rule; checking only the particular reviewer's own
authorship is a candidate, but exact absence and fencing are still unproved.
Individual target reviews would have different semantics and are not approved.

PO-07's closure is a write decision about all admitted participants. The earlier
candidate's participant-wide close votes and segmented coordinator still make
that write depend on every child; moving outcomes into small records or scanning
them incrementally does not solve the design issue. Neither is an endorsed
solution. Define a local-write closure that preserves the rule, or explicitly
resolve a change to the product rule. Existing PO-07 behavior remains in force
until that decision is made.

An independently maintained read view can display each person's status and an
as-of readiness indication. It must distinguish a complete source frontier from
partial or unknown audience capture; no known blocker from an incomplete view
does not establish readiness. A lead's close request combined with a derived,
possibly revisable readiness status is an alternative, not equivalent to the
current terminal `CampaignClosed` fact. In particular it does not by itself
determine whether a delayed join belongs to the open campaign, prevent a late
amendment, or identify the irreversible closing instant. If the product keeps
that distinction, the authoritative transition still needs a separate solution.

The business time used for any final decision must be agreed and proven against
due dates and any applicable waiver semantics. A person may remain pending but
be past due, so PO-07 can allow closure without their completion. Existing
exception rules do not establish automatic reversal when a waiver expires. An
exact retry of one participant's source observation is a no-op; a later distinct
observation may need a new amendment. Unknown outcomes require recovery and
delayed operations need proper source/target fencing. None of these per-person
rules proves complete audience capture or closure.

One possible launch design records an exact roster boundary once, then derives
each person's origin from their own facts and creates the participant
independently. A complete, replayable boundary and gapless transition are
prerequisites to this design, not facts established here. This does not put a
roster in the campaign or make launch perform a write decision over all people;
the audience is a read reconstructed from individual facts. History/corrections,
qualifying roster attributes and completeness still need source qualification.

## Jev review, 2026-11-05

Live TypeSafe documentation was consulted for the System One HTTP request.
The synthetic cases and exact request are in
[policy-campaign-boundaries-jev-request.json](policy-campaign-boundaries-jev-request.json);
the complete response is in
[policy-campaign-boundaries-jev-response.json](policy-campaign-boundaries-jev-response.json).
The model was `jev-1.14.0`. Values below are the Noul probability of the stated
case, not proofs of the source or protocol.

| Case | Probability |
| --- | ---: |
| Individual launch facts reconstruct an audience given a complete source cut | 0.88 |
| A delayed roster projection omits a qualifying person | 0.95 |
| Deterministic IDs alone ensure the omitted person is created | 0.07 |
| A delayed pre-close join can be missed by a close of known participants | 0.90 |
| A person remains in the audience while another relationship qualifies | 0.91 |
| Participant-stream concurrency serializes one person's competing operations | 0.94 |
| A delayed unfenced command can still be accepted after timeout | 0.88 |
| The due-date closing predicate can differ at two different times | 0.91 |
| The proposed campaign holds a growing participant list | 0.05 |
| Naming missing guarantees proves they exist | 0.03 |

The candidate split and explicit blockers are supported, but these artificial
examples do not establish real storage behavior or demonstrate a correct
end-to-end closure protocol. No domain build, tests, or global validation were
run as part of this review.

### Local decision and readiness follow-up

The separate [request](policy-campaign-local-jev-request.json) and
[response](policy-campaign-local-jev-response.json) test concrete PO-07 and
audience examples with `jev-1.14.0`. The answers are advisory, including:

| Proposition | Probability |
| --- | ---: |
| An overdue pending participant must complete before closure under PO-07 | 0.07 |
| The conditional all-clear holds when the only pending participant is overdue | 0.84 |
| A view omitting an admitted, within-due-date participant is reliable | 0.05 |
| A close request proves the closing condition was satisfied | 0.07 |
| Closure eligibility can change as a due date passes without a new outcome | 0.97 |
| One person's membership is locally decidable under the stipulated exact source boundary | 0.97 |
| A revisable dashboard status is equivalent to the recorded terminal close | 0.03 |
| M0-D12 permits deriving the audience from application-access assignments | 0.02 |

Jev gave 0.47 to whether the hypothetical launch example establishes the
actual infrastructure guarantee; that judgment is inconclusive, and the answer
is still no from source reasoning: the example merely states an assumption. A
0.06 result on whether one of two qualifying relationships ending necessarily
removes the person supports retaining the other relationship, not a claim about
how the source provides atomic person-level eligibility. These responses do not
prove an implementable local closure, source consistency or concurrency safety.

### Independently owned role targets

The [request](policy-campaign-target-jev-request.json) and
[response](policy-campaign-target-jev-response.json) examine candidate target
and reviewer authority with `jev-1.14.0`. Jev returned 0.91 that the sole target
could be removed between an advisory read and approval, 0.07 that the earlier
read proves nonempty approval, and 0.06 that independently addressable targets
alone freeze their exact version membership. It rated a single reviewer's
authoritative keyed lookup at 0.96 *under a stipulated fence* and individual
target review satisfying the existing whole-proposal reviewer rule at 0.14.
A hash alone recovering absent target facts scored 0.02. The stipulated source
and fence are not established by the prompt or model output; target admission,
review, immutable version and recovery remain open.
