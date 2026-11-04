# AccessControl Cassie source batch

Prepared immutable head `d0d29064a4bb19bb71a95eaac8d1cb83e795bc0c`, based on `adcc1348`; apply `f6cdc122` then `d0d29064`. Workforce and held Member work are separate.

Seven native repositories replace the six directory stores and permission authorizer. The feature schema has eight tables, eight secondary indexes, and only single-column primary keys. Seven row keys structurally length-encode full framework projection identity plus business IDs; graph state uses its full projection identity as the primary key. Permission graph remains JSONB; simple directory values use typed columns. Existing unsigned assignment revisions use invariant TEXT to preserve their entire ulong range.

The permission graph reducer, permission catalog and Member owners are unchanged. Current membership checks, explainable built-in graph path, pending-revocation scan, final grant reread and authoritative Member eligibility gate remain in order. Team membership reads retain pending event-tail decisions. Only the six explicitly authorized list-handler catch clauses changed; Validation status and messages remain.

Test-first source commit authors 28 native boundaries. Final prepared source includes 59 new authored cases, including 24 native paging combinations and full unsigned revision/normalization regressions. All 800 prior AccessControl test method names remain. No .NET build, test or formatter ran. Whitespace diff checks passed.

Jev 1.14 source review raised a broad pagination signal of .70. Narrowed mechanisms were .36 wrong last position, .35 skipped lookahead, .17 ascending predicate, .15 descending predicate, .10 equal-name ties, .05 substring narrowing; all24 authored paging combinations were recognized at .92. Source reasoning: lastSort/lastId update only for included rows; extra match breaks before capture; strict continuation revisits the unreturned extra match. Unique-ID shapes now use single-key continuation and names include their ID tie. These tests must execute before qualification; the advisory signal is recorded, not hidden. Provider API concern .34 after exact pinned source; direct API source matches, native SQL behavior remains unqualified.

See handoff JSON for exact interface wiring, schema, remaining external callers, handlers and evidence. Coordinator owns shared DI, startup/config/packages, fixtures, catalog paths and caller changes.


## Native compatibility corrections after coordinator execution

Current prepared head `226d00d6`; apply sequence `f6cdc122`, `d0d29064`, `6d880189`, `226d00d6`. Batch remains held pending first-nine native gates.

Coordinator native tests proved two published Cassie0.1 constraints: LIMIT cannot be a bound parameter, and INSERT VALUES does not accept CAST expressions. `6d880189` now emits an invariant numeric LIMIT derived only from clamped1..200 plus one lookahead row. `226d00d6` changes permission graph storage to TEXT with a direct bound JSON string and plain SELECT payload. JSON serializer, reducer and permission checks are unchanged. Earlier source-only API/JSONB discussion is historical and superseded; it did not establish engine SQL compatibility.

No .NET build/test/format ran in this worker. Existing authored native tests remain unchanged for coordinator execution.
