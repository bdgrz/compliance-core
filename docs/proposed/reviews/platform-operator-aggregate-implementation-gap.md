# Platform operator aggregate implementation gap

Status: **replacement-first and two-primary decisions recorded; qualification gaps remain**,
2026-11-05. No aggregate, event contract,
handler, source authority, migration, or provider is implemented by this review.

The criteria admission design requires an exact current operator-grant owner for
the authenticated user, but its two-pass source proof is conditional on a
qualified current source and does not define or approve that owner's complete
lifecycle. See [criteria authorization](criteria-authorization-design.md).

The [platform access context](../contexts/platform-access.md#platformoperator-proposed)
proposes a per-user `PlatformOperator` and coordinating primary authority. The
user has now approved replacement first and at most two confirmed or pending
primaries, while ordinary operators remain unlimited. The
[selected two-seat protocol](platform-operator-primary-protocol.md)
records the resulting owner decisions. Bootstrap eligibility, source
qualification, unknown-commit recovery and migration also remain unqualified.

The existing accepted product rule is that an operator may grant or revoke
status, subject to preserving at least one operator after bootstrap, and that
configured seeds cannot regrant a revoked operator on restart. The current
`PlatformOperatorRoster` remains the currently registered authority. A per-user
record alone cannot enforce the last-operator rule and must not be activated
before the selected seat protocol and a safe authority cutover are implemented
and independently qualified.

Every designated primary consumes one of the two protected seats; the previously
considered unlimited-primary keeper subset is not selected. Review exact event names and schemas after the
owner transitions are selected; implement and qualify operation identity,
fencing, bootstrap, migration and cutover without a window of dual authority.
The existing Jev review evaluates traces, not event names or provider guarantees.

No compilation or tests were run.
