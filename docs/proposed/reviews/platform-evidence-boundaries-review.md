# Platform access and evidence boundary review

Status: **historical candidate review**, 2026-11-05. The user subsequently
approved replacement first and at most two confirmed or pending primaries.
The [current protocol and alternatives](platform-operator-primary-protocol.md)
record that distinction. This review does not claim source implementation or
executable qualification.

## Findings and candidates

| Existing shape | Invariant to preserve | Candidate and remaining obligation |
| --- | --- | --- |
| `PlatformOperators` appends every operator grant and revocation to one stream. | At least one platform operator remains after bootstrap, with no fixed cap on ordinary operators; replacement first and at most two confirmed or pending primaries are approved. | One operator owner per user plus two protected seats is selected. Bootstrap, fencing, recovery and migration still require qualified implementation. |
| Invitation event stores every chosen team and program; member registration copies all guest programs. | One pending offer per email; guest programs limit access and guest offers have at least one program. | Independently owned selections and per-episode program grants. An exact program lookup can prove one grant only if current episode authority also holds. An independently owned grant alone does not enforce a minimum or prove a sealed selection set. Specify selection admission, closure, transfer, revocation and late-command outcomes, and whether activation waits for team outcomes. Do not fabricate a cross-stream source cut. |
| Offboarding completed event carries every attest package delivery. | Complete only after client export receipt, every required attest delivery and closure of every accepted service engagement. | Keep deliveries and engagement outcomes with their owners; completion carries at most a reference to proof. A source-complete, admission-fenced protocol to construct that proof is still absent. A zero projected count, unfinished dispatch or incomplete scan does not prove completion. |
| `EvidenceRequestFulfilled` lists every artifact ID. | One request is open, fulfilled or cancelled and at least one available artifact is required to fulfil. | One available anchor on the request, independent request-artifact and existing artifact-to-record support links for additional artifacts. Qualify availability against concurrent disposal or quarantine. Confirm whether accepting a multi-artifact submission must be atomic before prescribing admission/completion; do not assume it away. |
| Artifact disclosure checked by a prior read and recorded asynchronously. | At actual disclosure the artifact is allowed to be shared, and the recorded handling class is the one applicable then. | A delivery-time authoritative ordering/fence must cover artifact state, delivery and relevant release/revocation. The exact protocol is not specified. Recording an event after release cannot retroactively authorize it. Coordinate with Retention and Auditor collaboration. |
| Member–Person mapping spans contexts. | One member has at most one current Person; one Person can correlate to multiple members. | `Member` owns its single optional Person identity and generation on the same stream as episode changes. Exact episode/generation fences prevent stale unlink. Correlation retention across deprovisioning is a candidate policy, not a grant; product confirmation and common command/event contracts remain. |

Other known authority gaps are immediate authorization revocation across tenant,
role, team and membership owners, and account recovery racing a new identity
attachment. These require explicit ordering and cannot be established from
projection absence. Independent owners do not by themselves solve those races.
Some actual customer evidence files and export contents are business artifacts;
this review does not treat their external bytes as aggregate state or an excuse
for embedding a population in a domain event.

## Primary-seat decision and recovery

Both the existing organization-primary policy (PA-27) and the proposed platform
operator policy need exact designation generations. Each owner keeps at most two
current seats. The individually governed Member or PlatformOperator owns its
current mark. Each reservation, mark, refusal, confirmation, release and unmark
binds the exact seat, target and generation; Member marks also bind an episode.

| Situation | Required outcome |
| --- | --- |
| Mark and revoke/suspend race on the same target | The target stream orders the two. If removal wins, the mark is refused; if the mark wins, removal is refused while that mark is protected. |
| Mark accepted but reply is lost | Seat stays pending. Reconcile the exact generation; a timeout cannot count it as accepted or free the reservation. |
| Mark definitively refused | Preserve a terminal refusal or equivalent target-side fence. A delayed command with that generation must not succeed after the target's eligibility changes. |
| Same user is released and later designated again | A delayed unmark for the first generation must not clear the new generation. An unmark naming merely the user is unsafe. |
| Both confirmed primaries are released concurrently | The seat owner orders releases and refuses the one that would leave no confirmed replacement. |
| Configured bootstrap races or restarts | One initial reservation wins; a target grant and mark must be confirmed before initialization is ready. Once bootstrap completed, configuration must not regrant a revoked user. |

The proposed platform-operator seat protocol is still a candidate. Atomic
target-owned marks, exact retry and changed-intent handling, a refusal fence on an
empty or existing target, unknown-commit recovery, initialization authorization
and migration are not established by these tables. Primary-seat decisions are
low churn and form the owner's own invariant history; retaining that history is
not a reason to split the owner or introduce a replacement mechanism.

The platform-primary policy changes an operator's workflow: an active
ordinary operator cannot be treated as a replacement until explicitly marked
and confirmed. The user has now approved replacement first and a maximum of two
confirmed or pending platform primaries. The PA-27
organization-primary workflow already requires a confirmed replacement, but
the proposed extra details are not a claim of full native or provider acceptance.
The [organization implementation review](organization-bootstrap.md) records
existing designation and episode identifiers and their focused tests. The
existing [PrimaryOrgAdmins owner](../../../src/Compliance.Core/Features/AccessControl/Aggregates/PrimaryOrgAdmins.cs)
protects the organization's one-or-two-primary invariant; its infrequent
designation and release facts are appropriate business history. Ordinary
administrator membership is not accumulated there. Native and provider
acceptance are separate qualification questions.

## Jev evidence

Exact [request](platform-evidence-boundaries-jev-request.json) and
[response](platform-evidence-boundaries-jev-response.json), `jev-1.14.0`,
2026-11-05. Each number below is the returned Noul probability for the literal
question in the request, not a probability that a protocol is correct.

| Judgment | Probability |
| --- | ---: |
| Operator stream grows with every independent grant and revoke | 0.90 |
| Conditional primary-seat rule can preserve at least one operator | 0.94 |
| Sole primary protection can add a designation step | 0.92 |
| Unrestricted team and program selection can grow one invitation event | 0.95 |
| Exact episode-program grant can enforce a particular program when the correct episode is also confirmed | 0.79 |
| Separate grants alone ensure the guest minimum under concurrent removal | 0.09 |
| Listing all artifacts can grow one fulfilment event | 0.94 |
| One verified anchor plus independently owned support links avoids a request artifact list | 0.80 |
| Availability read can be stale at later disclosure | 0.95 |
| Lagging projected zero proves no accepted open engagement | 0.05 |
| Engagement acceptance can race offboarding using only a prior tenant read | 0.76 |

These answers flag useful counterexamples and conditional designs. They do not
establish that an admission set is complete, that a provider supports a source
cut or fence, that an operation has a definitive outcome after a lost response,
or that the candidate operator workflow is approved. Those remain open until
supported by a concrete protocol, source guarantees and adversarial tests.

### Targeted generation and replay review

The exact synthetic scenarios and `jev-1.14.0` answer are in the
[request](platform-workforce-local-jev-request.json) and
[response](platform-workforce-local-jev-response.json). A delayed unmark
removing a later designation scored 0.73, inconclusive; the written trace
demonstrates the failure when only current marked status is checked. A target
generation check scored 0.97, timeout proving a mark never committed 0.04,
fixed-seat replay growth over rotations 0.93, and the added designation step
0.85. These are advisory judgments, not proof that storage, recovery, or the
candidate workflow meets its obligations. The replay-growth question describes
the existence of lifecycle events, not a defect or a reason to redesign the
low-churn organization-primary owner.
