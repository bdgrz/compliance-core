# Workforce boundary review

2026-11-05. Scope: WF-05 member/person association and WF-07/08 source
observation ordering. This is a design review, not implementation or proof of
framework behavior. [Current proposal](../contexts/workforce-assurance.md).

## Findings and disposition

1. The Person-owned member list placed arbitrarily many independently governed
   members and link facts on one Person while failing to serialize competing
   Persons for the same Member. Move the single optional Person reference to
   `Member`; build the reverse list as a projection. The Member's versioned
   stream resolves competing links, episode changes, and stale unlink attempts.
   A permanent same-tenant `PersonRecorded` fact is a stable existence proof;
   authorization is still checked separately against current membership.
2. Separate observation streams cannot decide the greatest accepted revision
   by reading a projection. One source-record authority serializes admission;
   each observation owns its own pending/decision. Each target owns its own
   source binding, revision fence and application outcome. Neither the source
   owner nor the Person owns a participant roster.
3. Admission and target updates are separate commits. A target can temporarily
   contain an earlier admitted revision after a newer admission. Revision
   fencing guarantees no regression only after the newer revision is applied
   at that target. Unknown outcomes remain pending. Complete source-owned
   snapshots prevent skipped intermediate deltas from losing data; user-owned
   fields are preserved. Source collisions require resolution.
4. The latest admission alone cannot prove whether an old retry succeeded.
   Durable admission evidence and recovery by request identity are required;
   the implementation mechanism is unqualified. Do not represent a stale
   request as an exact retry simply because its revision is old.
5. After Member links Person P with L1, unlinks and relinks P with L3, current
   Person and generation cannot establish L1's original outcome or intent. An
   exact retry returns L1's historical result without changing the current L3
   link. An authoritative retained Member fact or equally authoritative lookup
   must distinguish L1 from changed intent; an unresolved outcome remains
   pending. This does not authorize storing a child roster, and replay of the
   Member's own growing history still needs operational qualification.

## Jev evidence

Two direct TypeSafe System One calls returned `jev-1.14.0`. Exact
[first request](workforce-boundaries-jev-request.json) and
[response](workforce-boundaries-jev-response.json), and
[follow-up request](workforce-boundaries-followup-jev-request.json) and
[response](workforce-boundaries-followup-jev-response.json) are preserved.
No personal data was sent. Values are Noul probabilities of the exact question
being true, not probabilities that the architecture is correct.

| Concrete question | Probability | Assessment |
| --- | ---: | --- |
| Two different current links win on one versioned Member | 0.03 | Supports the shared-owner constraint. |
| Person needs a roster to enforce at most one Person per Member | 0.16; follow-up 0.24 | Supports independent Member ownership, second answer less decisive. |
| Stale generation unlink removes a later link | 0.13 | Supports generation checks. |
| A historical identity link authorizes a new membership episode | 0.06 | Supports separate authorization. |
| Older revision newly accepted after higher gate admission | 0.13 | Supports serialized admission, subject to actual atomic append. |
| Target can already contain revision 5 merely because gate accepted it | 0.03; follow-up 0.09 | Supports independently tracked outcomes. |
| Revision 4 applies after 5 is admitted but before 5 reaches target | 0.38; follow-up 0.60 | Inconclusive. The explicit event ordering permits it; do not claim immediate target fencing. |
| Revision 4 overwrites already applied 5 at fenced target | 0.03; follow-up 0.29 | First answer supports the fence; follow-up is less decisive. Verify the actual target implementation. |
| Latest gate revision alone identifies an earlier exact admission retry | 0.10 | Supports durable historical outcome lookup. |
| Skipping delta revision 4 can lose a change | 0.87 | Supports complete snapshots. |
| Full revision 5 snapshot can yield correct source-owned values without applying 4 | 0.96 | Supports full snapshots for out-of-order delivery. |

Other answers were mixed: reading a stale projection as sufficient to accept
concurrent revisions scored 0.56; replacing user-owned fields destroying facts
scored 0.50. Both are mechanically unsafe in the documented counterexamples,
so those answers are unresolved signals, not authority for either shortcut.
Likewise, Jev does not validate whether a framework can perform the required
atomic append, preserve outcomes, or provide indexed recovery.

A later local review is recorded in
[request](platform-workforce-local-jev-request.json) and
[response](platform-workforce-local-jev-response.json). Jev scored current
Person and generation alone proving an older operation's exact outcome 0.05,
the retained Person correlation alone granting a later episode access 0.04, and
an old-episode unlink being valid for the later episode 0.03. These outputs are
advisory and do not establish authoritative historical lookup or episode rules.

## Qualification before implementation

- Confirm whether identity correlation survives deprovisioning and rejoining;
  regardless of that choice, old episode commands cannot alter the new episode.
- Confirm ordered source revisions (including manual entries), duplicate
  content identity and conflict handling for each provider.
- Define ownership and precedence of every source/user field, and source
  binding and migration when two source records target the same Person. A
  single authoritative source per target is a deliberate proposed constraint,
  not a fact established by existing requirements.
- Qualify atomic compare-and-append at gate and target; durable, bounded-memory
  recovery of an older admitted operation after later admissions; and idempotent
  per-target outcomes, including unknown commits and crash recovery.
- Exercise competing links, unlink/relink and old episodes; concurrent and
  reordered admission, duplicate/changed retries, dismissal racing pending
  admission, target creation collisions, partial target failure and reordered
  full snapshots. Snapshotting or archival may be needed for a source record's
  own growing history; no fixed lifetime event-stream bound is claimed.
