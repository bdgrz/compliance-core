# AccessGrant events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-grant.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AccessGrantIssued | AccessGrantIssued | business | bdgrz.access-grant.issued | 1 | [Placeholder: schema absent] | A role grant was issued to a principal for a scope, with its source and effective period, optionally tied to the membership episode that gave rise to it. |
| AccessGrantRevoked | AccessGrantRevoked | business | bdgrz.access-grant.revoked | 1 | [Placeholder: schema absent] | An issued access grant was revoked by a named actor at a recorded time, ending the access it conferred. |

## Event details

### AccessGrantIssued

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessGrantIssued |
| Selected source event name | AccessGrantIssued |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessGrantIssued |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessGrantIssued |
| Adopted wire | `{"discriminator":"bdgrz.access-grant.issued","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/AccessGrantIssued.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A role grant was issued to a principal for a scope, with its source and effective period, optionally tied to the membership episode that gave rise to it. |
| Selected source meaning | A role grant was issued to a principal for a scope, with its source and effective period, optionally tied to the membership episode that gave rise to it. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/AccessGrantIssued.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S006](../reactors/retired-member-grant-reactor.md#s006) | physical root, logical prerequisite | AccessGrant | RemoveRetiredMemberAccessAndAssignments | AccessGrant |
| [S028](../reactors/tenant-invitation-reactor.md#s028) | emitted semantic fact | TenantInvitation | ProvisionGuestProgramAccess | AccessGrant |

### AccessGrantRevoked

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessGrantRevoked |
| Selected source event name | AccessGrantRevoked |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessGrantRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessGrantRevoked |
| Adopted wire | `{"discriminator":"bdgrz.access-grant.revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/AccessGrantRevoked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An issued access grant was revoked by a named actor at a recorded time, ending the access it conferred. |
| Selected source meaning | An issued access grant was revoked by a named actor at a recorded time, ending the access it conferred. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/AccessGrantRevoked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S005](../reactors/member-retirement-reactor.md#s005) | emitted semantic fact | Member | RemoveRetiredMemberAccessAndAssignments | AccessGrant |
| [S006](../reactors/retired-member-grant-reactor.md#s006) | emitted semantic fact | AccessGrant | RemoveRetiredMemberAccessAndAssignments | AccessGrant |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
