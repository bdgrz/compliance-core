# Application inventory boundary review

2026-11-05. Jev `jev-1.14.0`; exact [request](inventory-boundaries-jev-request.json) and [response](inventory-boundaries-jev-response.json). These cases are review signals, not proofs or evidence that the runtime implements the required protocol.

## Local decision reassessment

The former source-cut completion proposal still made retirement or merge
completion depend on every active instance and is withdrawn. Separate instance
outcome records and bounded pages do not change that decision boundary. AI-01
still calls for moving instances and aliases; AI-02 still refuses retirement
when active instances exist. Their local-authority design remains unresolved.

[M0-D05](../decisions/m0-d05-application-inventory.md) expressly describes
tenant-unique aliases and unique active provider instance keys, whereas AI-03
had treated both as advisory. The accepted uniqueness requirements remain in
force. An authoritative uniqueness decision needs an appropriately scoped key
owner and release/retry rules; an eventually consistent duplicate gap cannot
enforce it. The ownership protocol is not yet qualified.

A candidate successor relationship would let an instance retain its own original
application while a read resolves the effective survivor. That avoids a parent
operation per instance, but does not record the physical move in the instance
stream and therefore changes AI-01. It requires explicit historical, access-owner
and alias semantics. Concurrent reciprocal merges can form a cycle; target
retirement, late declarations and unknown outcomes require real authority. A
fixed ID ranking would rule out some user-selected survivors. A view cannot
prove those guarantees. A merge chain also has growing traversal time even if
no chain is loaded into memory.

For ordinary retirement, a one-time source marker that admissions have ever
opened can compete with retirement on the application stream, without an event
for each instance. Restricting retirement to applications whose admission marker
was never set changes AI-02 by refusing even when every past instance has since
retired. A failed first admission may leave a conservative marker; target
retirement and successor races remain unresolved. Neither alternative is an
approved policy or a qualified provider protocol.

The separate [request](inventory-local-invariants-jev-request.json) and
[response](inventory-local-invariants-jev-response.json) rated effective display
through a redirect 0.97, a physical move having been recorded 0.09, a reciprocal
redirect cycle 0.94, the target-retirement race 0.92, and an asynchronous zero
omitting an instance 0.95. These are model signals about explicit scenarios,
not proof that either candidate preserves the product guarantees.

| Case | Jev probability | Interpretation |
| --- | ---: | --- |
| Original retire-before-move sequence can satisfy both rules | 0.21 | No: AI-02 refuses retirement while the source instance is active, while the old move guard required the source already retired. This explicit contradiction stands independently of the score. |
| Delayed accepted creation can commit after source retirement without a fence | 0.88 | Source closure must settle or irrevocably fence every earlier accepted admission. |
| Active target read can race with target retirement | 0.85 | A read is not admission. The target must protect each exact accepted move through its terminal outcome. |
| Target hold protects the exact move while held | 0.95 | Conditional on a durable, enforced hold; this case does not establish that the required mechanism exists. |
| Timed-out, unfenced command can commit after its hold is released | 0.88 | Unknown is pending. Reconcile or irrevocably fence before releasing the hold. |
| Sealed complete cut plus terminal participant facts excludes a remaining active source instance | 0.73 | Inconclusive model signal; the conclusion is conditional on complete membership, universal writer fencing, and authentic exact outcomes. None is yet qualified in the runtime, and no model score proves them. |
| Independent alias records still require a growing parent alias list | 0.10 | Alias ownership can be per association; the synthetic request's advisory-uniqueness premise conflicts with M0-D05 and is not the accepted product rule. |
| Stable cursor and bounded pages require a complete in-memory result | 0.03 | Page-by-page enumeration can avoid it if the source actually supplies the required stable order and cut. |
| Naming missing source and participant guarantees proves they exist | 0.03 | Explicitly retain them as implementation blockers. |

The prior `ApplicationMerged` event reported aliases in bulk and retired the source before moving active instances. The intermediate pending flow preserved the intent but still required a collection-wide completion decision; that mechanism is withdrawn. Long histories and individual event sizes must be bounded without replacing the parent roster with a long-lived per-child event stream.

Qualification must include at least these independently reproducible traces: an admission accepted before source closure but committed late; target close racing an admitted move; timeout followed by delayed acceptance; participant refusal and retry; an instance retiring or moving concurrently; reciprocal merges and a survivor later retiring; duplicate delivery and changed intent; a missed or stale page; alias addition racing closure; and a crash after each durable transition. Exact participant identities and versions, source ordering and terminal facts or durable fences need proof without an all-child write decision. The external access-governance projections must continue tracking the same instance identities and must label effective versus historical membership correctly.
