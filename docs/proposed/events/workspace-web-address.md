# WorkspaceWebAddress events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/workspace-web-address.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| WorkspaceWebAddressClaimed | TenantSlugRegistered | business | bdgrz.tenant-slug.registered | 1 | [Placeholder: schema absent] | The global address owner claimed this normalized public web address for one workspace. |
| WorkspaceWebAddressClaimDenied | TenantSlugRegistrationRejected | business | bdgrz.tenant-slug.registration-rejected | 1 | [Placeholder: schema absent] | The global owner refused the address claim because it was reserved, already belonged to another workspace, or had been permanently retired. |
| WorkspaceWebAddressReleaseDenied | TenantSlugSurrenderRejected | business | bdgrz.tenant-slug.surrender-rejected | 1 | [Placeholder: schema absent] | The global address owner refused to retire the workspace’s current public address. |
| WorkspaceWebAddressRetired | TenantSlugSurrendered | business | bdgrz.tenant-slug.surrendered | 1 | [Placeholder: schema absent] | The global owner permanently retired the surrendered address; it cannot be claimed again. |

## Event details

### WorkspaceWebAddressClaimed

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkspaceWebAddressClaimed |
| Selected source event name | TenantSlugRegistered |
| Name decision | Names the completed claim of a public address for one workspace. |
| Canonical semantic key | event:TenantSlugRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugRegistered |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The global address owner claimed this normalized public web address for one workspace. |
| Selected source meaning | The slug is claimed for the tenant. The fact is the native acceptance of the claim on the slug owner. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S022](../reactors/keep-workspace-web-addresses-unique-reactor.md#s022) | physical root, logical prerequisite | WorkspaceWebAddress | ConfirmWorkspaceWebAddress | Tenant |
| [S023](../reactors/keep-workspace-web-addresses-unique-reactor.md#s023) | physical root | WorkspaceWebAddress | RejectWorkspaceWebAddressRequest | Tenant |
| [S024](../reactors/keep-workspace-web-addresses-unique-reactor.md#s024) | physical root | WorkspaceWebAddress | ConfirmWorkspaceWebAddressRelease | Tenant |
| [S025](../reactors/keep-workspace-web-addresses-unique-reactor.md#s025) | physical root | WorkspaceWebAddress | RejectWorkspaceWebAddressRelease | Tenant |
| [S030](../reactors/establish-tenant-and-invite-owner-reactor.md#s030) | emitted semantic fact | Tenant | ClaimWorkspaceWebAddress | WorkspaceWebAddress |
| [S032](../reactors/establish-tenant-and-invite-owner-reactor.md#s032) | emitted semantic fact | Tenant | ClaimWorkspaceWebAddress | WorkspaceWebAddress |

### WorkspaceWebAddressClaimDenied

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkspaceWebAddressClaimDenied |
| Selected source event name | TenantSlugRegistrationRejected |
| Name decision | Names the global owner’s refusal to claim a public address. |
| Canonical semantic key | event:TenantSlugRegistrationRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugRegistrationRejected |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.registration-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistrationRejected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The global owner refused the address claim because it was reserved, already belonged to another workspace, or had been permanently retired. |
| Selected source meaning | The slug cannot be claimed for the tenant; the fact answers exactly one claim operation. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistrationRejected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S022](../reactors/keep-workspace-web-addresses-unique-reactor.md#s022) | physical root | WorkspaceWebAddress | ConfirmWorkspaceWebAddress | Tenant |
| [S023](../reactors/keep-workspace-web-addresses-unique-reactor.md#s023) | physical root, logical prerequisite | WorkspaceWebAddress | RejectWorkspaceWebAddressRequest | Tenant |
| [S024](../reactors/keep-workspace-web-addresses-unique-reactor.md#s024) | physical root | WorkspaceWebAddress | ConfirmWorkspaceWebAddressRelease | Tenant |
| [S025](../reactors/keep-workspace-web-addresses-unique-reactor.md#s025) | physical root | WorkspaceWebAddress | RejectWorkspaceWebAddressRelease | Tenant |
| [S030](../reactors/establish-tenant-and-invite-owner-reactor.md#s030) | emitted semantic fact | Tenant | ClaimWorkspaceWebAddress | WorkspaceWebAddress |
| [S032](../reactors/establish-tenant-and-invite-owner-reactor.md#s032) | emitted semantic fact | Tenant | ClaimWorkspaceWebAddress | WorkspaceWebAddress |

### WorkspaceWebAddressReleaseDenied

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkspaceWebAddressReleaseDenied |
| Selected source event name | TenantSlugSurrenderRejected |
| Name decision | Names the global owner’s refusal to retire the workspace’s current address. |
| Canonical semantic key | event:TenantSlugSurrenderRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrenderRejected |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.surrender-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRejected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The global address owner refused to retire the workspace’s current public address. |
| Selected source meaning | The tenant does not own the slug; the fact answers exactly one surrender operation. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRejected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S022](../reactors/keep-workspace-web-addresses-unique-reactor.md#s022) | physical root | WorkspaceWebAddress | ConfirmWorkspaceWebAddress | Tenant |
| [S023](../reactors/keep-workspace-web-addresses-unique-reactor.md#s023) | physical root | WorkspaceWebAddress | RejectWorkspaceWebAddressRequest | Tenant |
| [S024](../reactors/keep-workspace-web-addresses-unique-reactor.md#s024) | physical root | WorkspaceWebAddress | ConfirmWorkspaceWebAddressRelease | Tenant |
| [S025](../reactors/keep-workspace-web-addresses-unique-reactor.md#s025) | physical root, logical prerequisite | WorkspaceWebAddress | RejectWorkspaceWebAddressRelease | Tenant |
| [S033](../reactors/establish-tenant-and-invite-owner-reactor.md#s033) | emitted semantic fact | Tenant | RetireWorkspaceWebAddress | WorkspaceWebAddress |

### WorkspaceWebAddressRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkspaceWebAddressRetired |
| Selected source event name | TenantSlugSurrendered |
| Name decision | Names the permanent retirement of a surrendered public address. |
| Canonical semantic key | event:TenantSlugSurrendered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrendered |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.surrendered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrendered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The global owner permanently retired the surrendered address; it cannot be claimed again. |
| Selected source meaning | The tenant gave up the slug and the slug is retired for good. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrendered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S022](../reactors/keep-workspace-web-addresses-unique-reactor.md#s022) | physical root | WorkspaceWebAddress | ConfirmWorkspaceWebAddress | Tenant |
| [S023](../reactors/keep-workspace-web-addresses-unique-reactor.md#s023) | physical root | WorkspaceWebAddress | RejectWorkspaceWebAddressRequest | Tenant |
| [S024](../reactors/keep-workspace-web-addresses-unique-reactor.md#s024) | physical root, logical prerequisite | WorkspaceWebAddress | ConfirmWorkspaceWebAddressRelease | Tenant |
| [S025](../reactors/keep-workspace-web-addresses-unique-reactor.md#s025) | physical root | WorkspaceWebAddress | RejectWorkspaceWebAddressRelease | Tenant |
| [S033](../reactors/establish-tenant-and-invite-owner-reactor.md#s033) | emitted semantic fact | Tenant | RetireWorkspaceWebAddress | WorkspaceWebAddress |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
