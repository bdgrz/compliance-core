# TenantInvitationDeliveryProcess

- Classification: `technical_process_or_storage_owner`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `false`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** [Placeholder: selected target identity is not recorded.]

**Invariant:** Technical delivery process with one immutable effect payload and multiple attempt records. Attempt identity is state, never a new unconstrained process owner.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Retry same invitation ID with exact terms returns original acceptance; changed terms conflict. New invitation generation is independent. Delivery retries preserve exact token and accepted at-least-once policy.

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

[Placeholder: no selected commands are listed for this owner.]

## Events

[Placeholder: no canonical events are listed for this owner.]

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T117](../reactors/tenant-invitation-delivery-process-reactor.md#t117) | reaction_or_regular_process_stage | TenantInvitation | TenantInvitationDeliveryProcess | SendTenantInvitation |
| [T118](../reactors/tenant-invitation-delivery-process-reactor.md#t118) | reaction_or_regular_process_stage | TenantInvitation | TenantInvitationDeliveryProcess | RecordTenantInvitationDeliverySent |
| [T119](../reactors/tenant-invitation-delivery-process-reactor.md#t119) | reaction_or_regular_process_stage | TenantInvitation | TenantInvitationDeliveryProcess | RecordTenantInvitationDeliveryFailed |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/tenant-invitation-delivery-process.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `[Placeholder: implementation status not recorded]`.
