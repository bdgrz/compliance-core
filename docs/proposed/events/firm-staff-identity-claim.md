# FirmStaffIdentityClaim events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/firm-staff-identity-claim.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| FirmStaffIdentityClaimed | FirmStaffIdentityClaimed | business | bdgrz.firm-staff-identity-claim.firm-staff-identity-claimed | 1 | 8 | The permanent claim of one canonical user by one staff record. The claiming staff member is the root operation id of Operation; the claim is never released. |

## Event details

### FirmStaffIdentityClaimed

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffIdentityClaimed |
| Selected source event name | FirmStaffIdentityClaimed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:FirmStaffIdentityClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffIdentityClaimed |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-identity-claim.firm-staff-identity-claimed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffIdentityClaimed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The permanent claim of one canonical user by one staff record. The claiming staff member is the root operation id of Operation; the claim is never released. |
| Selected source meaning | The permanent claim of one canonical user by one staff record. The claiming staff member is the root operation id of Operation; the claim is never released. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffIdentityClaimed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffIdentityClaimed |
| Schema discriminator | bdgrz.firm-staff-identity-claim.firm-staff-identity-claimed |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffIdentityClaim |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Owner | EventStreamAddress | true |
| Revision | long | true |
| Generation | long | true |
| Intent | ImmutableContentRef | true |
| SourceProofs | ImmutableContentRef? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: firm-staff-registration-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | firm-staff-registration-intent.v1 |
| recordType | FirmStaffRegistrationIntent |
| rule | Freeze normalized source registration values and authoritative authenticated operator. Claim canonical user permanently before completing staff native registration. Conflicting claim rejects pending registration and fences late completion; status changes cannot replace user/practice/source. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| StaffMemberId | Uuid | true |
| CanonicalUserId | Uuid | true |
| Practice | string | true |
| SourceReference | string | true |
| InitialStatus | string | true |

##### Variant: FirmStaffRegistrationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| StaffMemberId | Uuid | true |
| CanonicalUserId | Uuid | true |
| Practice | string | true |
| SourceReference | string | true |
| InitialStatus | string | true |


#### Resolved intent schema

FirmStaffRegistrationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T002](../reactors/firm-staff-registration-reactor.md#t002) | emitted semantic fact | FirmStaffRecord | ClaimFirmStaffIdentity | FirmStaffIdentityClaim |
| [T003](../reactors/firm-staff-registration-reactor.md#t003) | logical prerequisite | FirmStaffIdentityClaim | CompleteFirmStaffRegistration | FirmStaffRecord |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
