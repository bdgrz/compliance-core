# FirmStaffRecord events

Canonical events: **7**. Complete canonical event and available schema records: [owner event JSON](../data/events/firm-staff-record.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| FirmStaffChangeRecorded | FirmStaffChangeRecorded | business | bdgrz.firm-staff.changed | 1 | 9 | One change of one staff record: its registration completing or a status change. The complete FirmStaffMemberView after the change is immutable content; no cumulative directory is carried. Intent is always the record's frozen registration intent. |
| FirmStaffProfessionalDutyAuthorized | FirmStaffProfessionalDutyUseAuthorized | reservation | bdgrz.firm-staff-record.firm-staff-professional-duty-use-authorized | 1 | 8 | An exact currently active canonical staff record is held for recording a professional-duty designation. The use does not advance the public staff content revision; Revision is the held revision. |
| FirmStaffProfessionalDutyAuthorizationClosed | FirmStaffProfessionalDutyUseSettled | reservation | bdgrz.firm-staff-record.firm-staff-professional-duty-use-settled | 1 | 8 | The staff use for one professional-duty change ended, after the recorded designation or after the rejection fence. A settlement before any grant fences the key against late acquisition. |
| FirmStaffRecordEngagementAuthorized | FirmStaffRecordEngagementUseAuthorized | reservation | bdgrz.firm-staff-record.firm-staff-record-engagement-use-authorized | 1 | 8 | Holds the exact active registered staff record the partner evaluation relies on; the use does not advance the public revision, so Revision is the held revision. |
| FirmStaffRecordEngagementAuthorizationClosed | FirmStaffRecordEngagementUseSettled | reservation | bdgrz.firm-staff-record.firm-staff-record-engagement-use-settled | 1 | 8 | The use of the staff record ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| FirmStaffRegistrationAccepted | FirmStaffRegistrationAccepted | business | bdgrz.firm-staff-record.firm-staff-registration-accepted | 1 | 8 | A firm staff registration was accepted for a new staff record; registration remains incomplete until the staff member's user identity is claimed. |
| FirmStaffRegistrationRejected | FirmStaffRegistrationRejected | business | bdgrz.firm-staff-record.firm-staff-registration-rejected | 1 | 8 | A pending registration was rejected because another staff record owns the canonical user. |

## Event details

### FirmStaffChangeRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffChangeRecorded |
| Selected source event name | FirmStaffChangeRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:FirmStaffChangeRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffChangeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff.changed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffChangeRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | One change of one staff record: its registration completing or a status change. The complete FirmStaffMemberView after the change is immutable content; no cumulative directory is carried. Intent is always the record's frozen registration intent. |
| Selected source meaning | One change of one staff record: its registration completing or a status change. The complete FirmStaffMemberView after the change is immutable content; no cumulative directory is carried. Intent is always the record's frozen registration intent. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffChangeRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffChangeRecorded |
| Schema discriminator | bdgrz.firm-staff.changed |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/FirmStaffChangeRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured FirmStaffMemberView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Rename register/status Operation to ChangeKind so typed operation provenance is unambiguous., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| OperationId | Uuid | true |
| ChangeKind | string | true |
| Reason | string? | true |
| StaffContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: FirmStaffChangeRecorded.StaffContent.v1

| Property | Value |
| --- | --- |
| field | StaffContent |
| schema | FirmStaffChangeRecorded.StaffContent.v1 |
| contentType | FirmStaffMemberView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

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
| [T003](../reactors/firm-staff-registration-reactor.md#t003) | emitted semantic fact | FirmStaffIdentityClaim | CompleteFirmStaffRegistration | FirmStaffRecord |
| [S017](../reactors/reassess-firm-staff-independence-after-changes-reactor.md#s017) | physical root, logical prerequisite | FirmStaffRecord | RecordDirectoryIndependenceReevaluation | ServiceEngagementIndependence |

### FirmStaffProfessionalDutyAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffProfessionalDutyAuthorized |
| Selected source event name | FirmStaffProfessionalDutyUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:FirmStaffProfessionalDutyUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffProfessionalDutyUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-record.firm-staff-professional-duty-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffProfessionalDutyUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An exact currently active canonical staff record is held for recording a professional-duty designation. The use does not advance the public staff content revision; Revision is the held revision. |
| Selected source meaning | An exact currently active canonical staff record is held for recording a professional-duty designation. The use does not advance the public staff content revision; Revision is the held revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffProfessionalDutyUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffProfessionalDutyUseAuthorized |
| Schema discriminator | bdgrz.firm-staff-record.firm-staff-professional-duty-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
| Schema status | selected target schema; implementation pending |
| Schema reason | Exact currently active canonical staff authority through designation recording |
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

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T113](../reactors/apply-or-revoke-professional-duty-designation-reactor.md#t113) | emitted semantic fact | FirmProfessionalDutyDesignation | AuthorizeFirmStaffProfessionalDuty | FirmStaffRecord |

### FirmStaffProfessionalDutyAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffProfessionalDutyAuthorizationClosed |
| Selected source event name | FirmStaffProfessionalDutyUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:FirmStaffProfessionalDutyUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffProfessionalDutyUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-record.firm-staff-professional-duty-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffProfessionalDutyUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The staff use for one professional-duty change ended, after the recorded designation or after the rejection fence. A settlement before any grant fences the key against late acquisition. |
| Selected source meaning | The staff use for one professional-duty change ended, after the recorded designation or after the rejection fence. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffProfessionalDutyUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffProfessionalDutyUseSettled |
| Schema discriminator | bdgrz.firm-staff-record.firm-staff-professional-duty-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
| Schema status | selected target schema; implementation pending |
| Schema reason | Exact currently active canonical staff authority through designation recording |
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

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T114](../reactors/apply-or-revoke-professional-duty-designation-reactor.md#t114) | emitted semantic fact | FirmProfessionalDutyDesignation | CloseFirmStaffProfessionalDutyAuthorization | FirmStaffRecord |

### FirmStaffRecordEngagementAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffRecordEngagementAuthorized |
| Selected source event name | FirmStaffRecordEngagementUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:FirmStaffRecordEngagementUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffRecordEngagementUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-record.firm-staff-record-engagement-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRecordEngagementUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Holds the exact active registered staff record the partner evaluation relies on; the use does not advance the public revision, so Revision is the held revision. |
| Selected source meaning | Holds the exact active registered staff record the partner evaluation relies on; the use does not advance the public revision, so Revision is the held revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRecordEngagementUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffRecordEngagementUseAuthorized |
| Schema discriminator | bdgrz.firm-staff-record.firm-staff-record-engagement-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
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

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T089](../reactors/engagement-acceptance-reactor.md#t089) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeFirmStaffRecordEngagement | FirmStaffRecord |

### FirmStaffRecordEngagementAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffRecordEngagementAuthorizationClosed |
| Selected source event name | FirmStaffRecordEngagementUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:FirmStaffRecordEngagementUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffRecordEngagementUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-record.firm-staff-record-engagement-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRecordEngagementUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The use of the staff record ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Selected source meaning | The use of the staff record ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRecordEngagementUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffRecordEngagementUseSettled |
| Schema discriminator | bdgrz.firm-staff-record.firm-staff-record-engagement-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
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

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T090](../reactors/engagement-acceptance-reactor.md#t090) | emitted semantic fact | ServiceEngagementIndependence | CloseFirmStaffRecordEngagementAuthorization | FirmStaffRecord |

### FirmStaffRegistrationAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffRegistrationAccepted |
| Selected source event name | FirmStaffRegistrationAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:FirmStaffRegistrationAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffRegistrationAccepted |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-record.firm-staff-registration-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRegistrationAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A firm staff registration was accepted for a new staff record; registration remains incomplete until the staff member's user identity is claimed. |
| Selected source meaning | A firm staff registration was accepted for a new staff record; registration remains incomplete until the staff member's user identity is claimed. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/FirmStaffRegistrationAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffRegistrationAccepted |
| Schema discriminator | bdgrz.firm-staff-record.firm-staff-registration-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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
| [T002](../reactors/firm-staff-registration-reactor.md#t002) | physical root, logical prerequisite | FirmStaffRecord | ClaimFirmStaffIdentity | FirmStaffIdentityClaim |
| [T003](../reactors/firm-staff-registration-reactor.md#t003) | physical root | FirmStaffIdentityClaim | CompleteFirmStaffRegistration | FirmStaffRecord |
| [A001](../reactors/unassigned-domain-choreography.md#a001) | physical root, logical prerequisite | FirmStaffRecord | RejectFirmStaffRegistration | FirmStaffRecord |

### FirmStaffRegistrationRejected

| Canonical event detail | Value |
| --- | --- |
| Target event name | FirmStaffRegistrationRejected |
| Selected source event name | FirmStaffRegistrationRejected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:FirmStaffRegistrationRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmStaffRegistrationRejected |
| Adopted wire | `{"discriminator":"bdgrz.firm-staff-record.firm-staff-registration-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRegistrationRejected.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A pending registration was rejected because another staff record owns the canonical user. |
| Selected source meaning | A pending registration was rejected because another staff record owns the canonical user. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmStaffRegistrationRejected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmStaffRegistrationRejected |
| Schema discriminator | bdgrz.firm-staff-record.firm-staff-registration-rejected |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmStaffRecord |
| Schema status | selected target schema; implementation pending |
| Schema reason | source admission contract |
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
| [A001](../reactors/unassigned-domain-choreography.md#a001) | emitted semantic fact | FirmStaffRecord | RejectFirmStaffRegistration | FirmStaffRecord |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
