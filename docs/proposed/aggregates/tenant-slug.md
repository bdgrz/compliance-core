# TenantSlug

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of normalized slug. Selected CommandId binds exact claimant tenant and reservation episode; the existing normalization and UUID algorithm remain unchanged.

**Invariant:** One normalized global slug can belong to one tenant and once surrendered is permanently retired.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RegisterTenantSlug](../commands/tenant-slug.md#registertenantslug) | command | CommandId |
| [SurrenderTenantSlug](../commands/tenant-slug.md#surrendertenantslug) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantSlugRegistered](../events/tenant-slug.md#tenantslugregistered) | bdgrz.tenant-slug.registered | 1 |
| [TenantSlugRegistrationRejected](../events/tenant-slug.md#tenantslugregistrationrejected) | bdgrz.tenant-slug.registration-rejected | 1 |
| [TenantSlugSurrenderRejected](../events/tenant-slug.md#tenantslugsurrenderrejected) | bdgrz.tenant-slug.surrender-rejected | 1 |
| [TenantSlugSurrendered](../events/tenant-slug.md#tenantslugsurrendered) | bdgrz.tenant-slug.surrendered | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S022](../reactors/tenant-slug-reactor.md#s022) | business_receiver | TenantSlug | Tenant | ConfirmTenantSlug |
| [S023](../reactors/tenant-slug-reactor.md#s023) | business_receiver | TenantSlug | Tenant | RejectTenantSlug |
| [S024](../reactors/tenant-slug-reactor.md#s024) | business_receiver | TenantSlug | Tenant | ConfirmTenantSlugSurrender |
| [S025](../reactors/tenant-slug-reactor.md#s025) | business_receiver | TenantSlug | Tenant | RejectTenantSlugSurrender |
| [S030](../reactors/tenant-registration-reactor.md#s030) | business_receiver | Tenant | TenantSlug | RegisterTenantSlug |
| [S032](../reactors/tenant-registration-reactor.md#s032) | business_receiver | Tenant | TenantSlug | RegisterTenantSlug |
| [S033](../reactors/tenant-registration-reactor.md#s033) | business_receiver | Tenant | TenantSlug | SurrenderTenantSlug |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [tenant-slug-review-2026-10-29.md](../../product/definitive-aggregate-choreography/tenant-slug-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/tenant-slug.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
