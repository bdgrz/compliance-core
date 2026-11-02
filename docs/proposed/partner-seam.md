# Provider and external-source seams

This page separates governed provider/partner facts from future integrations that observe external systems. It captures current decisions and manual source paths without proposing a connector.

## Governed providers and subservice organizations

A [Provider](aggregates/provider.md) is a domain record about a service provider. Provider assurance reports, reviews, coverage gaps, and risk acceptance have their own selected owners: [provider-assurance-report](aggregates/provider-assurance-report.md), [provider-assurance-review](aggregates/provider-assurance-review.md), [provider-assurance-gap](aggregates/provider-assurance-gap.md), [provider-risk-acceptance](aggregates/provider-risk-acceptance.md). These records do not imply a live connection to the provider.

M0-D11 selects these rules: materiality is based on customer-data processing/storage or a critical-path role, not spend; material providers need current SOC 2 or ISO/IEC 27001 evidence, with a completed security questionnaire accepted when neither exists; material reviews recur annually; and subservice organizations default to carve-out with CSOCs represented as subservice-responsibility commitments. A carved-out subservice without a CSOC and a material provider without a current review are readiness gaps.

Decision source: [M0-D11](../product/decisions/m0-d11-vendors.md). The selected data-shape alignment to M0-D28 is recorded in that decision; exact source/catalog implementation mapping remains visible on the owner pages.

## External source and integration boundary

M0-D20 defers connector/import work in R1/R2 until the canonical data shape and storage internals are settled. The product remains usable through its manual path; future connectors are optional enhancements. Initial candidate sources are AWS accounts, the GitHub organization, the identity-provider tenant, and core SaaS admin access such as Slack and Jira.

For technology inventory, M0-D08 makes the governed Compliance inventory authoritative; cloud-console, MDM, and repository-host data are discovery-only. Its component, information-classification, and data-flow rules are included in the proposed domain model.

The manual workforce observation path is separately captured in [workforce source governance](../product/workforce-source-governance.md). It records observed external facts, explicit correlation, authority labels, and human reconciliation. Provider observations can corroborate service-identity facts but do not assign governed owner, purpose, or review date.

A future source adapter must write observations through the canonical source shape and the staged all-or-nothing acceptance boundary described by [ADR 0005](../architecture/decisions/0005-import-reconciliation-and-background-processing.md). A callback or status endpoint is an observation, not proof of complete partner state.

## Connector proposal record

M0-D20 requires a proposal to answer the recorded integration questions below before connector implementation. The source heading says ten questions, but it numbers thirteen; all thirteen are preserved here pending reconciliation.

1. Which business question does the source answer?
2. Is it authoritative, corroborating, or discovery-only?
3. What are its stable identifiers, scope boundaries, timestamps, pagination, and deletion or tombstone behavior?
4. How is a complete population proven, and how is partial collection detected?
5. How do records match without silently merging identities or systems?
6. How do changes become reviewable proposals rather than automatic truth?
7. What are the least-privilege, credential-custody, rate-limit, retry, and revocation models?
8. What freshness and reconciliation cadence is required, and who owns it?
9. Which raw snapshots and normalized facts must be retained for audit proof?
10. How does this integration make the existing manual workflow easier to start, maintain, or operate, and how does that workflow stay usable when the integration is unavailable or disabled?
11. Which partner-side writes can have an ambiguous outcome when a request or response is lost, and what idempotency key, lookup, or operator recovery path prevents an unsafe duplicate write?
12. Which states depend on callbacks, partner staff, or partner communications; how are missing, late, duplicate, out-of-order, and unknown signals detected and reconciled, and who owns each manual follow-up?
13. What contract version, authentication and signature rules, validation and product rules, limits, and environment differences have been verified against representative test cases? Which behaviors remain untestable or unconfirmed before launch?

A connector must not blindly repeat a partner-side write after an unknown response. It needs partner-supported idempotency, reliable lookup, or visible operator recovery. Verified contract/version, authentication, callbacks, state transitions, limits, retries, test cases, and remaining launch risks stay explicit.

## Source and unresolved details

- Product decision: [M0-D20](../product/decisions/m0-d20-integration-sources.md).
- Provider policy: [M0-D11](../product/decisions/m0-d11-vendors.md).
- Technology inventory decisions: [M0-D08](../product/decisions/m0-d08-technology-inventory.md).
- Workforce source contract: [workforce-source-governance.md](../product/workforce-source-governance.md).
- Canonical model: [canonical-entity-model.md](../product/canonical-entity-model.md).

[Placeholder: candidate-specific source authority, verified partner contract, authentication, credentials, limits, retries, cadence, acceptance tests, and operational owner are unknown until a connector is selected.]
