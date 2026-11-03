# WorkspaceWebAddress

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`
- Selected source owner: `TenantSlug`.

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
| [ClaimWorkspaceWebAddress](../commands/workspace-web-address.md#claimworkspacewebaddress) | command | CommandId |
| [RetireWorkspaceWebAddress](../commands/workspace-web-address.md#retireworkspacewebaddress) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkspaceWebAddressClaimed](../events/workspace-web-address.md#workspacewebaddressclaimed) | bdgrz.tenant-slug.registered | 1 |
| [WorkspaceWebAddressClaimDenied](../events/workspace-web-address.md#workspacewebaddressclaimdenied) | bdgrz.tenant-slug.registration-rejected | 1 |
| [WorkspaceWebAddressReleaseDenied](../events/workspace-web-address.md#workspacewebaddressreleasedenied) | bdgrz.tenant-slug.surrender-rejected | 1 |
| [WorkspaceWebAddressRetired](../events/workspace-web-address.md#workspacewebaddressretired) | bdgrz.tenant-slug.surrendered | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S022](../reactors/keep-workspace-web-addresses-unique-reactor.md#s022) | business_receiver | WorkspaceWebAddress | Tenant | ConfirmWorkspaceWebAddress |
| [S023](../reactors/keep-workspace-web-addresses-unique-reactor.md#s023) | business_receiver | WorkspaceWebAddress | Tenant | RejectWorkspaceWebAddressRequest |
| [S024](../reactors/keep-workspace-web-addresses-unique-reactor.md#s024) | business_receiver | WorkspaceWebAddress | Tenant | ConfirmWorkspaceWebAddressRelease |
| [S025](../reactors/keep-workspace-web-addresses-unique-reactor.md#s025) | business_receiver | WorkspaceWebAddress | Tenant | RejectWorkspaceWebAddressRelease |
| [S030](../reactors/establish-tenant-and-invite-owner-reactor.md#s030) | business_receiver | Tenant | WorkspaceWebAddress | ClaimWorkspaceWebAddress |
| [S032](../reactors/establish-tenant-and-invite-owner-reactor.md#s032) | business_receiver | Tenant | WorkspaceWebAddress | ClaimWorkspaceWebAddress |
| [S033](../reactors/establish-tenant-and-invite-owner-reactor.md#s033) | business_receiver | Tenant | WorkspaceWebAddress | RetireWorkspaceWebAddress |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/workspace-web-address.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
