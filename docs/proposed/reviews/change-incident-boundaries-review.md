# Change and incident relationship boundaries

Reviewed 2026-11-05 with Jev `jev-1.14.0` over synthetic cases; the exact
[request](change-incident-boundaries-jev-request.json) and
[response](change-incident-boundaries-jev-response.json) are retained. Values
are advisory semantic judgments, not a proof of the protocol or of runtime
behavior. The reviewed text preserves CI-01 through CI-05 as requirements while
changing the relationship storage and making the revised authority protocol
provisional. The former admission-window plus complete-author-history approach
below is withdrawn: it still makes a write decision traverse all relationship
changes.

| Question | Jev probability | Assessment |
| --- | ---: | --- |
| Independent links require the item to contain every link | 0.03 | Each relationship may be individually owned; this does not by itself prove an assessment is complete. |
| An uncoordinated link can race an assessment based on a projected list | 0.93 | Do not use projections as authoritative source or rely on a root revision check. |
| Streaming a complete frozen source cut detects a reviewer's prior authorship | 0.83 | Conditional on the complete, durable source history and the correct cut. |
| The streamed check must retain every author in memory | 0.10 | Compare the reviewer to each author one at a time; total work can still grow with history. |
| The root history alone proves the reviewer did not author a separate link | 0.14 | The item and relationship admissions must share a verifiably complete revision source. |
| A timed-out admitted link can succeed after the boundary seals | 0.95 | Unknown outcomes must remain pending. |
| A previously admitted link can first succeed after a correctly fenced freeze | 0.08 | This depends on the stated atomic fence, participant outcome and generation assumptions; it is not evidence they exist. |

## Local-invariant follow-up

The [new request](work-local-invariants-jev-request.json) and
[response](work-local-invariants-jev-response.json) test an early designated
assessor, individually restricted revision authors, an unsettled pre-marker
relationship, stale digest authorization, ordering memory and Portia replay.
Jev gave 0.17 to the explicitly restricted assessor having authored an accepted
revision without a waiver, 0.04 to future-only checks proving no prior authorship,
0.96 to the assessment being recorded before an unknown relationship outcome,
and 0.04 to the marker proving that the assessor reviewed that relationship.
These results are conditional semantic signals, not proof of atomic ordering.

Early designation is a candidate whose restrictions on authoring and reviewer
selection differ from the currently accepted workflow. Even if every author
restriction is correctly enforced, an independently accepted relationship and a
recorded assessment need proven source order, episode fencing, exact witness
authority and honest semantics for unsettled outcomes. Reviewer replacement,
waivers, target membership, late edits, retention, and crash/retry recovery remain
unresolved. A completed read projection is a report at its proven frontier; it
does not itself validate a write or show that the assessor read every item.
