# WorkRelationship named changes and sponsorship

Partial implementation under #1176, against the locked Workforce assurance context.

Identity remains tenant plus normalized stable worker identifier. The owner enforces
revision, lifecycle and self-reference rules for named manager, department, sponsor
and date corrections. Primary and observation projections consume every new fact.
Date corrections retain the original private ending reason; field grants govern
manager comparison and disclosure. Unsponsored is a read-time gap computed through
the existing observation catch-up and active internal eligibility query, without a
new fact or store.

The named-change behavioral RED executed 61 cases: 54 failed and seven passed.
The 13 sponsorship cases were authored before implementation but not executed
until the user-authorized assembled checkpoint. Neither authorship nor Jev source
review is recorded as an executed RED.

Live Jev resolved `jev-1.14.0`. Named-change adverse statements were 0.08–0.15,
except the ended-sponsor extra-guard question at 0.30; source and regression retain
the proposal's allowed ended sponsor change. Sponsorship adverse statements were
0.06–0.12. Coordinator wiring questions ranged 0.05–0.18. A 0.31 date-reason
candidate was disproved by all three source consumers updating only the dates.

Assembled Release build: zero warnings/errors. Affected business suites: 797 passed.
Full units and whitespace validation: pending. Exact batch/PR head is recorded in
the implementation progress ledger.

Remaining: initial-record/source provenance and reconciliation conformance, initial
pending/active and sponsor admission requirements, generic revision migration,
and explicit competing-append/whole-workflow qualification. No complete owner or
context claim.
