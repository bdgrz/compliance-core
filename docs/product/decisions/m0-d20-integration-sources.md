# M0-D20: Inventory, access, and evidence integration sources

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D20 #77](https://github.com/bdgrz/compliance/issues/77).

## Decision

No connectors are built in R1 or R2, and no import or integration work is
scheduled until the canonical data shape (M0-D28) and the storage internals
are settled. [T2-08 #38](https://github.com/bdgrz/compliance/issues/38) and
[T2-08a #137](https://github.com/bdgrz/compliance/issues/137) stay P2
hypotheses. Every feature remains operable through the product's manual path
when no connector or optional source-collection automation is enabled. Future
imports and integrations are enhancements to make program setup, maintenance,
and operation easier; they do not replace or gate the user-operated workflow.
Manual effort per source is measured during the first readiness engagement,
and connectors are ranked from that data in
[#342](https://github.com/bdgrz/compliance/issues/342).

The first candidate sources are the first reviewed systems:

- AWS accounts;
- the GitHub organization;
- the identity-provider tenant;
- core SaaS admin access (for example Slack and Jira).

## Integration template

Every candidate source must answer these ten questions before a connector is
proposed. They come from the gap analysis.

1. Which business question does the source answer?
2. Is it authoritative, corroborating, or discovery-only?
3. What are its stable identifiers, scope boundaries, timestamps, pagination,
   and deletion or tombstone behavior?
4. How is a complete population proven, and how is partial collection
   detected?
5. How do records match without silently merging identities or systems?
6. How do changes become reviewable proposals rather than automatic truth?
7. What are the least-privilege, credential-custody, rate-limit, retry, and
   revocation models?
8. What freshness and reconciliation cadence is required, and who owns it?
9. Which raw snapshots and normalized facts must be retained for audit proof?
10. How does this integration make the existing manual workflow easier to
    start, maintain, or operate, and how does that workflow stay usable when the
    integration is unavailable or disabled?
11. Which partner-side writes can have an ambiguous outcome when a request or
    response is lost, and what idempotency key, lookup, or operator recovery
    path prevents an unsafe duplicate write?
12. Which states depend on callbacks, partner staff, or partner communications;
    how are missing, late, duplicate, out-of-order, and unknown signals detected
    and reconciled, and who owns each manual follow-up?
13. What contract version, authentication and signature rules, validation and
    product rules, limits, and environment differences have been verified
    against representative test cases? Which behaviors remain untestable or
    unconfirmed before launch?

Any future connector writes source observations into the M0-D28 canonical shape
through the ADR 0005 staged, all-or-nothing acceptance. A connector never writes
governed facts directly.

Partner callbacks and status endpoints are observations, not proof that the
partner emitted every transition or that local state is current. A proposed
integration must define freshness and reconciliation for missing, late,
duplicate, out-of-order, and unrecognized signals. It must expose incomplete
coverage and partner-side work that is pending rather than presenting those
states as complete.

Writes to a partner can succeed even when the caller loses the response. A
connector must not blindly repeat such a write: it needs a partner-supported
idempotency mechanism, a reliable lookup to determine whether the write took
effect, or a visible operator recovery path. The contract record must also
identify partner-managed communications and manual steps that affect the
customer or the observed status.

Before implementation and launch, record the verified partner contract and
environment-specific behavior, including authentication and callback
verification, identifiers, status transitions, validation and product rules,
limits, retries, and representative test outcomes. Open questions remain
explicit launch risks; examples and assumptions are not verified behavior.
