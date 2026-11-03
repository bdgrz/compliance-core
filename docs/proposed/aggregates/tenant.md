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
| [ChangeWorkspaceWebAddress](../commands/tenant.md#changeworkspacewebaddress) | http | CommandId |
| [CompleteTenantOffboarding](../commands/tenant.md#completetenantoffboarding) | command | CommandId |
| [ConfirmWorkspaceWebAddress](../commands/tenant.md#confirmworkspacewebaddress) | command | CommandId |
| [ConfirmWorkspaceWebAddressRelease](../commands/tenant.md#confirmworkspacewebaddressrelease) | command | CommandId |
| [OffboardTenant](../commands/tenant.md#offboardtenant) | http | CommandId |
| [ReactivateTenant](../commands/tenant.md#reactivatetenant) | command | CommandId |
| [RegisterTenant](../commands/tenant.md#registertenant) | http | TenantId |
| [RejectWorkspaceWebAddressRequest](../commands/tenant.md#rejectworkspacewebaddressrequest) | command | CommandId |
| [RejectWorkspaceWebAddressRelease](../commands/tenant.md#rejectworkspacewebaddressrelease) | command | CommandId |
| [RequestWorkspaceWebAddressRelease](../commands/tenant.md#requestworkspacewebaddressrelease) | command | CommandId |
| [SuspendTenant](../commands/tenant.md#suspendtenant) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantActivated](../events/tenant.md#tenantactivated) | bdgrz.tenant.activated | 2 |
| [TenantOffboardingCompleted](../events/tenant.md#tenantoffboardingcompleted) | bdgrz.tenants.tenant-offboarding-completed | 1 |
| [TenantOffboardingStarted](../events/tenant.md#tenantoffboardingstarted) | bdgrz.tenant.offboarding.started | 2 |
| [TenantReactivated](../events/tenant.md#tenantreactivated) | bdgrz.tenant.reactivated | 2 |
| [TenantRegistered](../events/tenant.md#tenantregistered) | bdgrz.tenant.registered | 1 |
| [WorkspaceWebAddressChangeRejected](../events/tenant.md#workspacewebaddresschangerejected) | bdgrz.tenant-slug.change-rejected | 1 |
| [WorkspaceWebAddressChangeRequested](../events/tenant.md#workspacewebaddresschangerequested) | bdgrz.tenant-slug.change-requested | 1 |
| [WorkspaceWebAddressChanged](../events/tenant.md#workspacewebaddresschanged) | bdgrz.tenant-slug.changed | 1 |
| [WorkspaceWebAddressConfirmed](../events/tenant.md#workspacewebaddressconfirmed) | bdgrz.tenant-slug.confirmed | 1 |
| [WorkspaceWebAddressClaimRejected](../events/tenant.md#workspacewebaddressclaimrejected) | bdgrz.tenant-slug.rejected | 1 |
| [WorkspaceWebAddressReleaseConfirmed](../events/tenant.md#workspacewebaddressreleaseconfirmed) | bdgrz.tenant-slug-surrender.confirmed | 1 |
| [WorkspaceWebAddressReleaseRejected](../events/tenant.md#workspacewebaddressreleaserejected) | bdgrz.tenant-slug-surrender.failed | 1 |
| [WorkspaceWebAddressReleaseRequested](../events/tenant.md#workspacewebaddressreleaserequested) | bdgrz.tenant-slug.surrender-requested | 1 |
| [TenantSuspended](../events/tenant.md#tenantsuspended) | bdgrz.tenant.suspended | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S011](../reactors/set-up-starting-tenant-access-reactor.md#s011) | business_receiver | Tenant | Role | DefineRole |
| [S012](../reactors/set-up-starting-tenant-access-reactor.md#s012) | business_receiver | Tenant | Team | DefineTeam |
| [S013](../reactors/set-up-starting-tenant-access-reactor.md#s013) | business_receiver | Tenant | Member | RegisterMember |
| [S014](../reactors/set-up-starting-tenant-access-reactor.md#s014) | business_receiver | Tenant | TeamRole | AssignTeamRole |
| [S015](../reactors/set-up-starting-tenant-access-reactor.md#s015) | business_receiver | Tenant | RolePermission | AssignRolePermission |
| [S016](../reactors/set-up-starting-tenant-access-reactor.md#s016) | business_receiver | Tenant | TeamMember | AssignTeamMember |
| [S022](../reactors/keep-workspace-web-addresses-unique-reactor.md#s022) | business_receiver | WorkspaceWebAddress | Tenant | ConfirmWorkspaceWebAddress |
| [S023](../reactors/keep-workspace-web-addresses-unique-reactor.md#s023) | business_receiver | WorkspaceWebAddress | Tenant | RejectWorkspaceWebAddressRequest |
| [S024](../reactors/keep-workspace-web-addresses-unique-reactor.md#s024) | business_receiver | WorkspaceWebAddress | Tenant | ConfirmWorkspaceWebAddressRelease |
| [S025](../reactors/keep-workspace-web-addresses-unique-reactor.md#s025) | business_receiver | WorkspaceWebAddress | Tenant | RejectWorkspaceWebAddressRelease |
| [S029](../reactors/activate-membership-after-invitation-acceptance-reactor.md#s029) | business_receiver | TenantInvitation | Tenant | ActivateTenant |
| [S030](../reactors/establish-tenant-and-invite-owner-reactor.md#s030) | business_receiver | Tenant | WorkspaceWebAddress | ClaimWorkspaceWebAddress |
| [S031](../reactors/establish-tenant-and-invite-owner-reactor.md#s031) | business_receiver | Tenant | TenantInvitation | InviteTenantMember |
| [S032](../reactors/establish-tenant-and-invite-owner-reactor.md#s032) | business_receiver | Tenant | WorkspaceWebAddress | ClaimWorkspaceWebAddress |
| [S033](../reactors/establish-tenant-and-invite-owner-reactor.md#s033) | business_receiver | Tenant | WorkspaceWebAddress | RetireWorkspaceWebAddress |
| [S034](../reactors/tenant-self-service-activation-reactor.md#s034) | business_receiver | TeamMember | Tenant | ActivateTenant |

## Product rules

- **D03-09** — There is exactly one workspace per organization; programs and engagements provide sub-scoping. Decision: [m0-d03-roles-and-separation-of-duties.md](../decisions/m0-d03-roles-and-separation-of-duties.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/tenant.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
