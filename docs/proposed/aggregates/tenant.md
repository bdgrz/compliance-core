# Tenant

- Classification: `business_aggregate`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Caller TenantId is immutable tenant creation identity; lifecycle operations use caller CommandId.

**Invariant:** One tenant lifecycle activates only after its slug and first-administrator conditions; offboarding forbids new active service; one current slug transition admitted at a time. Lifecycle: registered becomes active only after the slug is confirmed and the first administrator is assigned; active and suspended alternate only by an acting platform operator; active or suspended may start offboarding; offboarding completes only after the attest-record package delivery decision and retention disposition are recorded; every other transition is refused and a repeat of the current state is a no-op.

**Additional decision rule:** There is exactly one workspace per organization, and the client organization is the only tenant boundary with tenant_id its sole technical name. Only platform operators suspend, reactivate or offboard, and suspension blocks every member and assigned firm staff while preserving records. Suspension stops new authenticated service for every member until an operator reactivates the tenant; reactivation requires the suspension to be the current state; offboarding starts only from active or suspended, forbids new active service, and completes only after the attest-record package delivery decision and retention disposition are recorded (M0-D25).

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ActivateTenant](../commands/tenant.md#activatetenant) | command | CommandId |
| [ChangeTenantSlug](../commands/tenant.md#changetenantslug) | http | CommandId |
| [CompleteTenantOffboarding](../commands/tenant.md#completetenantoffboarding) | command | CommandId |
| [ConfirmTenantSlug](../commands/tenant.md#confirmtenantslug) | command | CommandId |
| [ConfirmTenantSlugSurrender](../commands/tenant.md#confirmtenantslugsurrender) | command | CommandId |
| [OffboardTenant](../commands/tenant.md#offboardtenant) | http | CommandId |
| [ReactivateTenant](../commands/tenant.md#reactivatetenant) | command | CommandId |
| [RegisterTenant](../commands/tenant.md#registertenant) | http | TenantId |
| [RejectTenantSlug](../commands/tenant.md#rejecttenantslug) | command | CommandId |
| [RejectTenantSlugSurrender](../commands/tenant.md#rejecttenantslugsurrender) | command | CommandId |
| [RequestTenantSlugSurrender](../commands/tenant.md#requesttenantslugsurrender) | command | CommandId |
| [SuspendTenant](../commands/tenant.md#suspendtenant) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantActivated](../events/tenant.md#tenantactivated) | bdgrz.tenant.activated | 2 |
| [TenantOffboardingCompleted](../events/tenant.md#tenantoffboardingcompleted) | bdgrz.tenants.tenant-offboarding-completed | 1 |
| [TenantOffboardingStarted](../events/tenant.md#tenantoffboardingstarted) | bdgrz.tenant.offboarding.started | 2 |
| [TenantReactivated](../events/tenant.md#tenantreactivated) | bdgrz.tenant.reactivated | 2 |
| [TenantRegistered](../events/tenant.md#tenantregistered) | bdgrz.tenant.registered | 1 |
| [TenantSlugChangeRejected](../events/tenant.md#tenantslugchangerejected) | bdgrz.tenant-slug.change-rejected | 1 |
| [TenantSlugChangeRequested](../events/tenant.md#tenantslugchangerequested) | bdgrz.tenant-slug.change-requested | 1 |
| [TenantSlugChanged](../events/tenant.md#tenantslugchanged) | bdgrz.tenant-slug.changed | 1 |
| [TenantSlugConfirmed](../events/tenant.md#tenantslugconfirmed) | bdgrz.tenant-slug.confirmed | 1 |
| [TenantSlugRejected](../events/tenant.md#tenantslugrejected) | bdgrz.tenant-slug.rejected | 1 |
| [TenantSlugSurrenderConfirmed](../events/tenant.md#tenantslugsurrenderconfirmed) | bdgrz.tenant-slug-surrender.confirmed | 1 |
| [TenantSlugSurrenderFailed](../events/tenant.md#tenantslugsurrenderfailed) | bdgrz.tenant-slug-surrender.failed | 1 |
| [TenantSlugSurrenderRequested](../events/tenant.md#tenantslugsurrenderrequested) | bdgrz.tenant-slug.surrender-requested | 1 |
| [TenantSuspended](../events/tenant.md#tenantsuspended) | bdgrz.tenant.suspended | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S011](../reactors/tenant-rbac-bootstrap-reactor.md#s011) | business_receiver | Tenant | Role | DefineRole |
| [S012](../reactors/tenant-rbac-bootstrap-reactor.md#s012) | business_receiver | Tenant | Team | DefineTeam |
| [S013](../reactors/tenant-rbac-bootstrap-reactor.md#s013) | business_receiver | Tenant | Member | RegisterMember |
| [S014](../reactors/tenant-rbac-bootstrap-reactor.md#s014) | business_receiver | Tenant | TeamRole | AssignTeamRole |
| [S015](../reactors/tenant-rbac-bootstrap-reactor.md#s015) | business_receiver | Tenant | RolePermission | AssignRolePermission |
| [S016](../reactors/tenant-rbac-bootstrap-reactor.md#s016) | business_receiver | Tenant | TeamMember | AssignTeamMember |
| [S022](../reactors/tenant-slug-reactor.md#s022) | business_receiver | TenantSlug | Tenant | ConfirmTenantSlug |
| [S023](../reactors/tenant-slug-reactor.md#s023) | business_receiver | TenantSlug | Tenant | RejectTenantSlug |
| [S024](../reactors/tenant-slug-reactor.md#s024) | business_receiver | TenantSlug | Tenant | ConfirmTenantSlugSurrender |
| [S025](../reactors/tenant-slug-reactor.md#s025) | business_receiver | TenantSlug | Tenant | RejectTenantSlugSurrender |
| [S029](../reactors/tenant-invitation-reactor.md#s029) | business_receiver | TenantInvitation | Tenant | ActivateTenant |
| [S030](../reactors/tenant-registration-reactor.md#s030) | business_receiver | Tenant | TenantSlug | RegisterTenantSlug |
| [S031](../reactors/tenant-registration-reactor.md#s031) | business_receiver | Tenant | TenantInvitation | InviteTenantMember |
| [S032](../reactors/tenant-registration-reactor.md#s032) | business_receiver | Tenant | TenantSlug | RegisterTenantSlug |
| [S033](../reactors/tenant-registration-reactor.md#s033) | business_receiver | Tenant | TenantSlug | SurrenderTenantSlug |
| [S034](../reactors/tenant-self-service-activation-reactor.md#s034) | business_receiver | TeamMember | Tenant | ActivateTenant |

## Product rules

- **D03-09** — There is exactly one workspace per organization; programs and engagements provide sub-scoping. Source: [m0-d03-roles-and-separation-of-duties.md](../../product/decisions/m0-d03-roles-and-separation-of-duties.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [tenant-review-2026-10-29.md](../../product/definitive-aggregate-choreography/tenant-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/tenant.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
