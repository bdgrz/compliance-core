# Tenant events

Canonical events: **14**. Complete canonical event and available schema records: [owner event JSON](../data/events/tenant.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TenantActivated | TenantActivatedV2 | business | bdgrz.tenant.activated | 2 | [Placeholder: schema absent] | A native activation decision bound to the exact authored administrator, email and command identity. |
| TenantOffboardingCompleted | TenantOffboardingCompleted | business | bdgrz.tenants.tenant-offboarding-completed | 1 | [Placeholder: schema absent] | Records the TenantOffboardingCompleted domain fact for one tenant-owned subject revision. |
| TenantOffboardingStarted | TenantOffboardingStartedV2 | business | bdgrz.tenant.offboarding.started | 2 | [Placeholder: schema absent] | A platform operator started offboarding the tenant with a stated reason, beginning the wind-down of the tenant's service. |
| TenantReactivated | TenantReactivatedV2 | business | bdgrz.tenant.reactivated | 2 | [Placeholder: schema absent] | A platform operator reactivated a suspended tenant with a stated reason, restoring its service. |
| TenantRegistered | TenantRegistered | business | bdgrz.tenant.registered | 1 | [Placeholder: schema absent] | A tenant was registered with an owner user, name and slug, optionally naming its first administrator and whether activation is required. |
| TenantSlugChangeRejected | TenantSlugChangeRejected | business | bdgrz.tenant-slug.change-rejected | 1 | [Placeholder: schema absent] | A requested slug change ended because the slug owner refused the new slug. |
| TenantSlugChangeRequested | TenantSlugChangeRequested | business | bdgrz.tenant-slug.change-requested | 1 | [Placeholder: schema absent] | An active tenant requested changing its slug from the old to a new value; the change stays pending until completed and blocks a second slug change. |
| TenantSlugChanged | TenantSlugChanged | business | bdgrz.tenant-slug.changed | 1 | [Placeholder: schema absent] | A tenant adopted the slug its slug owner registered. The fact is the native acceptance of the confirming receiver. |
| TenantSlugConfirmed | TenantSlugConfirmed | business | bdgrz.tenant-slug.confirmed | 1 | [Placeholder: schema absent] | The slug owner registered the tenant's first slug and the tenant confirmed it. |
| TenantSlugRejected | TenantSlugRejected | business | bdgrz.tenant-slug.rejected | 1 | [Placeholder: schema absent] | The slug owner refused the tenant's first slug. |
| TenantSlugSurrenderConfirmed | TenantSlugSurrenderConfirmed | business | bdgrz.tenant-slug-surrender.confirmed | 1 | [Placeholder: schema absent] | The slug owner released the slug the tenant was surrendering. |
| TenantSlugSurrenderFailed | TenantSlugSurrenderFailed | business | bdgrz.tenant-slug-surrender.failed | 1 | [Placeholder: schema absent] | The slug owner refused the tenant's surrender, so the tenant keeps the slug. |
| TenantSlugSurrenderRequested | TenantSlugSurrenderRequested | business | bdgrz.tenant-slug.surrender-requested | 1 | [Placeholder: schema absent] | A tenant started surrendering its confirmed slug. |
| TenantSuspended | TenantSuspendedV2 | business | bdgrz.tenant.suspended | 2 | [Placeholder: schema absent] | A platform operator suspended an active tenant with a stated reason, blocking its service until it is reactivated. |

## Event details

### TenantActivated

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantActivated |
| Selected source event name | TenantActivatedV2 |
| Name decision | Names the tenant's completed activation without exposing the implementation version suffix. |
| Canonical semantic key | event:TenantActivated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantActivatedV2 |
| Adopted wire | `{"discriminator":"bdgrz.tenant.activated","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantActivatedV2.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A native activation decision bound to the exact authored administrator, email and command identity. |
| Selected source meaning | A native activation decision bound to the exact authored administrator, email and command identity. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantActivatedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S029](../reactors/tenant-invitation-reactor.md#s029) | emitted semantic fact | TenantInvitation | ActivateTenant | Tenant |
| [S034](../reactors/tenant-self-service-activation-reactor.md#s034) | emitted semantic fact | TeamMember | ActivateTenant | Tenant |

### TenantOffboardingCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantOffboardingCompleted |
| Selected source event name | TenantOffboardingCompleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantOffboardingCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantOffboardingCompleted |
| Adopted wire | `{"discriminator":"bdgrz.tenants.tenant-offboarding-completed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantOffboardingCompleted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the TenantOffboardingCompleted domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the TenantOffboardingCompleted domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantOffboardingCompleted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantOffboardingStarted

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantOffboardingStarted |
| Selected source event name | TenantOffboardingStartedV2 |
| Name decision | Names the start of tenant offboarding without exposing the implementation version suffix. |
| Canonical semantic key | event:TenantOffboardingStarted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantOffboardingStartedV2 |
| Adopted wire | `{"discriminator":"bdgrz.tenant.offboarding.started","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantOffboardingStartedV2.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A platform operator started offboarding the tenant with a stated reason, beginning the wind-down of the tenant's service. |
| Selected source meaning | A platform operator started offboarding the tenant with a stated reason, beginning the wind-down of the tenant's service. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/TenantOffboardingStartedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantReactivated

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantReactivated |
| Selected source event name | TenantReactivatedV2 |
| Name decision | Names the tenant's completed reactivation without exposing the implementation version suffix. |
| Canonical semantic key | event:TenantReactivated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantReactivatedV2 |
| Adopted wire | `{"discriminator":"bdgrz.tenant.reactivated","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantReactivatedV2.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A platform operator reactivated a suspended tenant with a stated reason, restoring its service. |
| Selected source meaning | A platform operator reactivated a suspended tenant with a stated reason, restoring its service. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/TenantReactivatedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantRegistered

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantRegistered |
| Selected source event name | TenantRegistered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantRegistered |
| Adopted wire | `{"discriminator":"bdgrz.tenant.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A tenant was registered with an owner user, name and slug, optionally naming its first administrator and whether activation is required. |
| Selected source meaning | A tenant was registered with an owner user, name and slug, optionally naming its first administrator and whether activation is required. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/TenantRegistered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S011](../reactors/tenant-rbac-bootstrap-reactor.md#s011) | physical root, logical prerequisite | Tenant | DefineRole | Role |
| [S012](../reactors/tenant-rbac-bootstrap-reactor.md#s012) | physical root, logical prerequisite | Tenant | DefineTeam | Team |
| [S013](../reactors/tenant-rbac-bootstrap-reactor.md#s013) | physical root, logical prerequisite | Tenant | RegisterMember | Member |
| [S014](../reactors/tenant-rbac-bootstrap-reactor.md#s014) | physical root, logical prerequisite | Tenant | AssignTeamRole | TeamRole |
| [S015](../reactors/tenant-rbac-bootstrap-reactor.md#s015) | physical root, logical prerequisite | Tenant | AssignRolePermission | RolePermission |
| [S016](../reactors/tenant-rbac-bootstrap-reactor.md#s016) | physical root, logical prerequisite | Tenant | AssignTeamMember | TeamMember |
| [S030](../reactors/tenant-registration-reactor.md#s030) | physical root, logical prerequisite | Tenant | RegisterTenantSlug | TenantSlug |
| [S031](../reactors/tenant-registration-reactor.md#s031) | physical root, logical prerequisite | Tenant | InviteTenantMember | TenantInvitation |
| [S032](../reactors/tenant-registration-reactor.md#s032) | physical root | Tenant | RegisterTenantSlug | TenantSlug |
| [S033](../reactors/tenant-registration-reactor.md#s033) | physical root | Tenant | SurrenderTenantSlug | TenantSlug |

### TenantSlugChangeRejected

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugChangeRejected |
| Selected source event name | TenantSlugChangeRejected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugChangeRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugChangeRejected |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.change-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugChangeRejected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A requested slug change ended because the slug owner refused the new slug. |
| Selected source meaning | A requested slug change ended because the slug owner refused the new slug. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugChangeRejected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S023](../reactors/tenant-slug-reactor.md#s023) | emitted semantic fact | TenantSlug | RejectTenantSlug | Tenant |

### TenantSlugChangeRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugChangeRequested |
| Selected source event name | TenantSlugChangeRequested |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugChangeRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugChangeRequested |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.change-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugChangeRequested.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An active tenant requested changing its slug from the old to a new value; the change stays pending until completed and blocks a second slug change. |
| Selected source meaning | An active tenant requested changing its slug from the old to a new value; the change stays pending until completed and blocks a second slug change. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/TenantSlugChangeRequested.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S030](../reactors/tenant-registration-reactor.md#s030) | physical root | Tenant | RegisterTenantSlug | TenantSlug |
| [S031](../reactors/tenant-registration-reactor.md#s031) | physical root | Tenant | InviteTenantMember | TenantInvitation |
| [S032](../reactors/tenant-registration-reactor.md#s032) | physical root, logical prerequisite | Tenant | RegisterTenantSlug | TenantSlug |
| [S033](../reactors/tenant-registration-reactor.md#s033) | physical root | Tenant | SurrenderTenantSlug | TenantSlug |

### TenantSlugChanged

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugChanged |
| Selected source event name | TenantSlugChanged |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugChanged |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugChanged |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.changed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugChanged.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A tenant adopted the slug its slug owner registered. The fact is the native acceptance of the confirming receiver. |
| Selected source meaning | A tenant adopted the slug its slug owner registered. The fact is the native acceptance of the confirming receiver. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugChanged.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S022](../reactors/tenant-slug-reactor.md#s022) | emitted semantic fact | TenantSlug | ConfirmTenantSlug | Tenant |
| [S030](../reactors/tenant-registration-reactor.md#s030) | physical root | Tenant | RegisterTenantSlug | TenantSlug |
| [S031](../reactors/tenant-registration-reactor.md#s031) | physical root | Tenant | InviteTenantMember | TenantInvitation |
| [S032](../reactors/tenant-registration-reactor.md#s032) | physical root | Tenant | RegisterTenantSlug | TenantSlug |
| [S033](../reactors/tenant-registration-reactor.md#s033) | physical root, logical prerequisite | Tenant | SurrenderTenantSlug | TenantSlug |

### TenantSlugConfirmed

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugConfirmed |
| Selected source event name | TenantSlugConfirmed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugConfirmed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugConfirmed |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.confirmed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugConfirmed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The slug owner registered the tenant's first slug and the tenant confirmed it. |
| Selected source meaning | The slug owner registered the tenant's first slug and the tenant confirmed it. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugConfirmed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S022](../reactors/tenant-slug-reactor.md#s022) | emitted semantic fact | TenantSlug | ConfirmTenantSlug | Tenant |

### TenantSlugRejected

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugRejected |
| Selected source event name | TenantSlugRejected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugRejected |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugRejected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The slug owner refused the tenant's first slug. |
| Selected source meaning | The slug owner refused the tenant's first slug. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugRejected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S023](../reactors/tenant-slug-reactor.md#s023) | emitted semantic fact | TenantSlug | RejectTenantSlug | Tenant |

### TenantSlugSurrenderConfirmed

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugSurrenderConfirmed |
| Selected source event name | TenantSlugSurrenderConfirmed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugSurrenderConfirmed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrenderConfirmed |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug-surrender.confirmed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderConfirmed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The slug owner released the slug the tenant was surrendering. |
| Selected source meaning | The slug owner released the slug the tenant was surrendering. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderConfirmed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S024](../reactors/tenant-slug-reactor.md#s024) | emitted semantic fact | TenantSlug | ConfirmTenantSlugSurrender | Tenant |

### TenantSlugSurrenderFailed

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugSurrenderFailed |
| Selected source event name | TenantSlugSurrenderFailed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugSurrenderFailed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrenderFailed |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug-surrender.failed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderFailed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The slug owner refused the tenant's surrender, so the tenant keeps the slug. |
| Selected source meaning | The slug owner refused the tenant's surrender, so the tenant keeps the slug. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderFailed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S025](../reactors/tenant-slug-reactor.md#s025) | emitted semantic fact | TenantSlug | RejectTenantSlugSurrender | Tenant |

### TenantSlugSurrenderRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugSurrenderRequested |
| Selected source event name | TenantSlugSurrenderRequested |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugSurrenderRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrenderRequested |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.surrender-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRequested.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A tenant started surrendering its confirmed slug. |
| Selected source meaning | A tenant started surrendering its confirmed slug. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRequested.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S030](../reactors/tenant-registration-reactor.md#s030) | physical root | Tenant | RegisterTenantSlug | TenantSlug |
| [S031](../reactors/tenant-registration-reactor.md#s031) | physical root | Tenant | InviteTenantMember | TenantInvitation |
| [S032](../reactors/tenant-registration-reactor.md#s032) | physical root | Tenant | RegisterTenantSlug | TenantSlug |
| [S033](../reactors/tenant-registration-reactor.md#s033) | physical root, logical prerequisite | Tenant | SurrenderTenantSlug | TenantSlug |

### TenantSuspended

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSuspended |
| Selected source event name | TenantSuspendedV2 |
| Name decision | Names the tenant's completed suspension without exposing the implementation version suffix. |
| Canonical semantic key | event:TenantSuspended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSuspendedV2 |
| Adopted wire | `{"discriminator":"bdgrz.tenant.suspended","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSuspendedV2.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A platform operator suspended an active tenant with a stated reason, blocking its service until it is reactivated. |
| Selected source meaning | A platform operator suspended an active tenant with a stated reason, blocking its service until it is reactivated. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/TenantSuspendedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
