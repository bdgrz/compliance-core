# TenantSlug events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/tenant-slug.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TenantSlugRegistered | TenantSlugRegistered | business | bdgrz.tenant-slug.registered | 1 | [Placeholder: schema absent] | The slug is claimed for the tenant. The fact is the native acceptance of the claim on the slug owner. |
| TenantSlugRegistrationRejected | TenantSlugRegistrationRejected | business | bdgrz.tenant-slug.registration-rejected | 1 | [Placeholder: schema absent] | The slug cannot be claimed for the tenant; the fact answers exactly one claim operation. |
| TenantSlugSurrenderRejected | TenantSlugSurrenderRejected | business | bdgrz.tenant-slug.surrender-rejected | 1 | [Placeholder: schema absent] | The tenant does not own the slug; the fact answers exactly one surrender operation. |
| TenantSlugSurrendered | TenantSlugSurrendered | business | bdgrz.tenant-slug.surrendered | 1 | [Placeholder: schema absent] | The tenant gave up the slug and the slug is retired for good. |

## Event details

### TenantSlugRegistered

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugRegistered |
| Selected source event name | TenantSlugRegistered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugRegistered |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The slug is claimed for the tenant. The fact is the native acceptance of the claim on the slug owner. |
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
| [S022](../reactors/tenant-slug-reactor.md#s022) | physical root, logical prerequisite | TenantSlug | ConfirmTenantSlug | Tenant |
| [S023](../reactors/tenant-slug-reactor.md#s023) | physical root | TenantSlug | RejectTenantSlug | Tenant |
| [S024](../reactors/tenant-slug-reactor.md#s024) | physical root | TenantSlug | ConfirmTenantSlugSurrender | Tenant |
| [S025](../reactors/tenant-slug-reactor.md#s025) | physical root | TenantSlug | RejectTenantSlugSurrender | Tenant |
| [S030](../reactors/tenant-registration-reactor.md#s030) | emitted semantic fact | Tenant | RegisterTenantSlug | TenantSlug |
| [S032](../reactors/tenant-registration-reactor.md#s032) | emitted semantic fact | Tenant | RegisterTenantSlug | TenantSlug |

### TenantSlugRegistrationRejected

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugRegistrationRejected |
| Selected source event name | TenantSlugRegistrationRejected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugRegistrationRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugRegistrationRejected |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.registration-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugRegistrationRejected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The slug cannot be claimed for the tenant; the fact answers exactly one claim operation. |
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
| [S022](../reactors/tenant-slug-reactor.md#s022) | physical root | TenantSlug | ConfirmTenantSlug | Tenant |
| [S023](../reactors/tenant-slug-reactor.md#s023) | physical root, logical prerequisite | TenantSlug | RejectTenantSlug | Tenant |
| [S024](../reactors/tenant-slug-reactor.md#s024) | physical root | TenantSlug | ConfirmTenantSlugSurrender | Tenant |
| [S025](../reactors/tenant-slug-reactor.md#s025) | physical root | TenantSlug | RejectTenantSlugSurrender | Tenant |
| [S030](../reactors/tenant-registration-reactor.md#s030) | emitted semantic fact | Tenant | RegisterTenantSlug | TenantSlug |
| [S032](../reactors/tenant-registration-reactor.md#s032) | emitted semantic fact | Tenant | RegisterTenantSlug | TenantSlug |

### TenantSlugSurrenderRejected

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugSurrenderRejected |
| Selected source event name | TenantSlugSurrenderRejected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugSurrenderRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrenderRejected |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.surrender-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrenderRejected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The tenant does not own the slug; the fact answers exactly one surrender operation. |
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
| [S022](../reactors/tenant-slug-reactor.md#s022) | physical root | TenantSlug | ConfirmTenantSlug | Tenant |
| [S023](../reactors/tenant-slug-reactor.md#s023) | physical root | TenantSlug | RejectTenantSlug | Tenant |
| [S024](../reactors/tenant-slug-reactor.md#s024) | physical root | TenantSlug | ConfirmTenantSlugSurrender | Tenant |
| [S025](../reactors/tenant-slug-reactor.md#s025) | physical root, logical prerequisite | TenantSlug | RejectTenantSlugSurrender | Tenant |
| [S033](../reactors/tenant-registration-reactor.md#s033) | emitted semantic fact | Tenant | SurrenderTenantSlug | TenantSlug |

### TenantSlugSurrendered

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantSlugSurrendered |
| Selected source event name | TenantSlugSurrendered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantSlugSurrendered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantSlugSurrendered |
| Adopted wire | `{"discriminator":"bdgrz.tenant-slug.surrendered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantSlugSurrendered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The tenant gave up the slug and the slug is retired for good. |
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
| [S022](../reactors/tenant-slug-reactor.md#s022) | physical root | TenantSlug | ConfirmTenantSlug | Tenant |
| [S023](../reactors/tenant-slug-reactor.md#s023) | physical root | TenantSlug | RejectTenantSlug | Tenant |
| [S024](../reactors/tenant-slug-reactor.md#s024) | physical root, logical prerequisite | TenantSlug | ConfirmTenantSlugSurrender | Tenant |
| [S025](../reactors/tenant-slug-reactor.md#s025) | physical root | TenantSlug | RejectTenantSlugSurrender | Tenant |
| [S033](../reactors/tenant-registration-reactor.md#s033) | emitted semantic fact | Tenant | SurrenderTenantSlug | TenantSlug |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
