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
All 8,327 backend units passed with zero failures/skips; whitespace validation passed. Exact batch/PR head is recorded in
the implementation progress ledger.

Remaining: initial-record/source provenance and reconciliation conformance, initial
pending/active and sponsor admission requirements, generic revision migration,
and explicit competing-append/whole-workflow qualification. No complete owner or
context claim.

## Batch 3 initial record conformance

Initial source provenance is retained through command, event, owner and primary
read model; stable worker identity is unchanged. New records admit only pending or
active, and naming a sponsor checks existing active internal eligibility. Exact
permitted retries retain their original source/terms even after sponsor departure
or observation lag. The hydrated restricted-field gate runs before retry decisions.
Legacy facts still replay; old test setup shortcuts now seed historical facts.

Forty-one cases were authored first and first executed in the assembled checkpoint;
no earlier behavioral RED is claimed. Live Jev source checks were advisory; an
uncertain restricted-oracle candidate was checked against the hydrated gate and
permission/no-permission retry cases. Final local suite: 8,431 passed, zero failures
or skips. Generic mutation retirement remains a separately prepared next slice;
Person link cardinality, ServiceIdentity conformance, source authority/reconciliation
and command-specific roles remain mapped in workforce-implementation.md.
