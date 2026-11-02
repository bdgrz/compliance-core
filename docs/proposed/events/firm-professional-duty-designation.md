# FirmProfessionalDutyDesignation events

Canonical events: **6**. Complete canonical event and available schema records: [owner event JSON](../data/events/firm-professional-duty-designation.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| FirmProfessionalDutyDesignationEngagementUseAuthorized | reservation | bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-authorized | 1 | 8 | Holds the exact active engagement-partner designation the partner evaluation relies on; the use does not advance the public revision, so Revision is the held revision. |
| FirmProfessionalDutyDesignationEngagementUseSettled | reservation | bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-settled | 1 | 8 | The use of the duty designation ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| FirmProfessionalDutyDesignationRecorded | business | bdgrz.firm.professional-duty.designation-recorded | 1 | 8 | A signed active assertion recorded or refreshed the designation. The complete FirmProfessionalDutyDesignationView is immutable content; no cumulative catalog is carried. |
| FirmProfessionalDutyDesignationRevoked | business | bdgrz.firm.professional-duty.designation-revoked | 1 | 8 | A signed revocation assertion ended the designation. The complete FirmProfessionalDutyDesignationView is immutable content; no cumulative catalog is carried. |
| ProfessionalDutyChangeAccepted | business | bdgrz.firm-professional-duty-designation.professional-duty-change-accepted | 1 | 8 | The root native acceptance of a professional-duty change. The designation changes only when the process claims the signed assertion and FirmProfessionalDutyDesignationRecorded or FirmProfessionalDutyDesignationRevoked follows. |
| ProfessionalDutyChangeRejected | business | bdgrz.firm-professional-duty-designation.professional-duty-change-rejected | 1 | 8 | A pending professional-duty change was rejected before any designation changed. The rejection fences the assertion claim and the apply step against late delivery. |

## Event details

### FirmProfessionalDutyDesignationEngagementUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FirmProfessionalDutyDesignationEngagementUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmProfessionalDutyDesignationEngagementUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationEngagementUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Holds the exact active engagement-partner designation the partner evaluation relies on; the use does not advance the public revision, so Revision is the held revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationEngagementUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmProfessionalDutyDesignationEngagementUseAuthorized |
| Schema discriminator | bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmProfessionalDutyDesignation |
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
| [T091](../reactors/engagement-acceptance-reactor.md#t091) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |

### FirmProfessionalDutyDesignationEngagementUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FirmProfessionalDutyDesignationEngagementUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmProfessionalDutyDesignationEngagementUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationEngagementUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The use of the duty designation ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationEngagementUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmProfessionalDutyDesignationEngagementUseSettled |
| Schema discriminator | bdgrz.firm-professional-duty-designation.firm-professional-duty-designation-engagement-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmProfessionalDutyDesignation |
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
| [T092](../reactors/engagement-acceptance-reactor.md#t092) | emitted semantic fact | ServiceEngagementIndependence | SettleFirmProfessionalDutyDesignationEngagementUse | FirmProfessionalDutyDesignation |

### FirmProfessionalDutyDesignationRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FirmProfessionalDutyDesignationRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmProfessionalDutyDesignationRecorded |
| Adopted wire | `{"discriminator":"bdgrz.firm.professional-duty.designation-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A signed active assertion recorded or refreshed the designation. The complete FirmProfessionalDutyDesignationView is immutable content; no cumulative catalog is carried. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmProfessionalDutyDesignationRecorded |
| Schema discriminator | bdgrz.firm.professional-duty.designation-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmProfessionalDutyDesignation |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/FirmProfessionalDutyDesignationRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured FirmProfessionalDutyDesignationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| OperationId | Uuid | true |
| DesignationContent | ImmutableContentRef | true |
| SignedAssertion | string | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: FirmProfessionalDutyDesignationRecorded.DesignationContent.v1

| Property | Value |
| --- | --- |
| field | DesignationContent |
| schema | FirmProfessionalDutyDesignationRecorded.DesignationContent.v1 |
| contentType | FirmProfessionalDutyDesignationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: professional-duty-change-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | professional-duty-change-intent.v1 |
| recordType | ProfessionalDutyChangeIntent |
| rule | AuthoredChange is the exact normalized RecordFirmProfessionalDutyDesignation or RevokeFirmProfessionalDutyDesignation field schema. Verified scope contains authority, tenant/staff/duty/source/valid interval and signed payload digest. Global assertion identity claim binds the complete verified tuple; independent proof never comes from a caller boolean. Recording additionally requires exact currently active canonical staff use, held through native designation; revocation does not require active staff. Acquire before irreversible assertion claim. Reject/fence before source settlement on definite pre-apply failure. Freeze and validate signed validity at the declared original decision instant. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |

##### Variant: ProfessionalDutyChangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |


#### Resolved intent schema

ProfessionalDutyChangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T001](../reactors/professional-duty-change-reactor.md#t001) | emitted semantic fact | ProfessionalAssertionIdentityClaim | ApplyProfessionalDutyChange | FirmProfessionalDutyDesignation |
| [T114](../reactors/professional-duty-change-reactor.md#t114) | logical prerequisite | FirmProfessionalDutyDesignation | SettleFirmStaffProfessionalDutyUse | FirmStaffRecord |

### FirmProfessionalDutyDesignationRevoked

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FirmProfessionalDutyDesignationRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FirmProfessionalDutyDesignationRevoked |
| Adopted wire | `{"discriminator":"bdgrz.firm.professional-duty.designation-revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationRevoked.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A signed revocation assertion ended the designation. The complete FirmProfessionalDutyDesignationView is immutable content; no cumulative catalog is carried. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/FirmProfessionalDutyDesignationRevoked.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FirmProfessionalDutyDesignationRevoked |
| Schema discriminator | bdgrz.firm.professional-duty.designation-revoked |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmProfessionalDutyDesignation |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/FirmProfessionalDutyDesignationRevoked.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured FirmProfessionalDutyDesignationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| OperationId | Uuid | true |
| DesignationContent | ImmutableContentRef | true |
| SignedAssertion | string | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: FirmProfessionalDutyDesignationRevoked.DesignationContent.v1

| Property | Value |
| --- | --- |
| field | DesignationContent |
| schema | FirmProfessionalDutyDesignationRevoked.DesignationContent.v1 |
| contentType | FirmProfessionalDutyDesignationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: professional-duty-change-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | professional-duty-change-intent.v1 |
| recordType | ProfessionalDutyChangeIntent |
| rule | AuthoredChange is the exact normalized RecordFirmProfessionalDutyDesignation or RevokeFirmProfessionalDutyDesignation field schema. Verified scope contains authority, tenant/staff/duty/source/valid interval and signed payload digest. Global assertion identity claim binds the complete verified tuple; independent proof never comes from a caller boolean. Recording additionally requires exact currently active canonical staff use, held through native designation; revocation does not require active staff. Acquire before irreversible assertion claim. Reject/fence before source settlement on definite pre-apply failure. Freeze and validate signed validity at the declared original decision instant. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |

##### Variant: ProfessionalDutyChangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |


#### Resolved intent schema

ProfessionalDutyChangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T001](../reactors/professional-duty-change-reactor.md#t001) | emitted semantic fact | ProfessionalAssertionIdentityClaim | ApplyProfessionalDutyChange | FirmProfessionalDutyDesignation |

### ProfessionalDutyChangeAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProfessionalDutyChangeAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProfessionalDutyChangeAccepted |
| Adopted wire | `{"discriminator":"bdgrz.firm-professional-duty-designation.professional-duty-change-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ProfessionalDutyChangeAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of a professional-duty change. The designation changes only when the process claims the signed assertion and FirmProfessionalDutyDesignationRecorded or FirmProfessionalDutyDesignationRevoked follows. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ProfessionalDutyChangeAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProfessionalDutyChangeAccepted |
| Schema discriminator | bdgrz.firm-professional-duty-designation.professional-duty-change-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmProfessionalDutyDesignation |
| Schema existingContract | null (recorded) |
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

#### Content schema: professional-duty-change-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | professional-duty-change-intent.v1 |
| recordType | ProfessionalDutyChangeIntent |
| rule | AuthoredChange is the exact normalized RecordFirmProfessionalDutyDesignation or RevokeFirmProfessionalDutyDesignation field schema. Verified scope contains authority, tenant/staff/duty/source/valid interval and signed payload digest. Global assertion identity claim binds the complete verified tuple; independent proof never comes from a caller boolean. Recording additionally requires exact currently active canonical staff use, held through native designation; revocation does not require active staff. Acquire before irreversible assertion claim. Reject/fence before source settlement on definite pre-apply failure. Freeze and validate signed validity at the declared original decision instant. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |

##### Variant: ProfessionalDutyChangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |


#### Resolved intent schema

ProfessionalDutyChangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T000](../reactors/professional-duty-change-reactor.md#t000) | physical root, logical prerequisite | FirmProfessionalDutyDesignation | ClaimProfessionalAssertionIdentity | ProfessionalAssertionIdentityClaim |
| [T001](../reactors/professional-duty-change-reactor.md#t001) | physical root | ProfessionalAssertionIdentityClaim | ApplyProfessionalDutyChange | FirmProfessionalDutyDesignation |
| [T113](../reactors/professional-duty-change-reactor.md#t113) | physical root, logical prerequisite | FirmProfessionalDutyDesignation | AuthorizeFirmStaffProfessionalDutyUse | FirmStaffRecord |
| [T114](../reactors/professional-duty-change-reactor.md#t114) | physical root | FirmProfessionalDutyDesignation | SettleFirmStaffProfessionalDutyUse | FirmStaffRecord |
| [A000](../reactors/unassigned-architecture-rows.md#a000) | physical root, logical prerequisite | FirmProfessionalDutyDesignation | RejectProfessionalDutyChange | FirmProfessionalDutyDesignation |

### ProfessionalDutyChangeRejected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProfessionalDutyChangeRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProfessionalDutyChangeRejected |
| Adopted wire | `{"discriminator":"bdgrz.firm-professional-duty-designation.professional-duty-change-rejected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ProfessionalDutyChangeRejected.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A pending professional-duty change was rejected before any designation changed. The rejection fences the assertion claim and the apply step against late delivery. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ProfessionalDutyChangeRejected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProfessionalDutyChangeRejected |
| Schema discriminator | bdgrz.firm-professional-duty-designation.professional-duty-change-rejected |
| Schema discriminatorVersion | 1 |
| Schema owner | FirmProfessionalDutyDesignation |
| Schema existingContract | null (recorded) |
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

#### Content schema: professional-duty-change-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | professional-duty-change-intent.v1 |
| recordType | ProfessionalDutyChangeIntent |
| rule | AuthoredChange is the exact normalized RecordFirmProfessionalDutyDesignation or RevokeFirmProfessionalDutyDesignation field schema. Verified scope contains authority, tenant/staff/duty/source/valid interval and signed payload digest. Global assertion identity claim binds the complete verified tuple; independent proof never comes from a caller boolean. Recording additionally requires exact currently active canonical staff use, held through native designation; revocation does not require active staff. Acquire before irreversible assertion claim. Reject/fence before source settlement on definite pre-apply failure. Freeze and validate signed validity at the declared original decision instant. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |

##### Variant: ProfessionalDutyChangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DesignationId | Uuid | true |
| ChangeKind | ProfessionalDutyChangeKind | true |
| AuthoredChange | ImmutableContentRef | true |
| SignedAssertion | string | true |
| VerifiedAssertionId | Uuid | true |
| AuthorityRevision | ulong | true |
| VerifiedScope | ImmutableContentRef | true |
| PreviousAssertion | SourceFactLocator? | true |
| StaffEligibility | SourceUseRequirement? | true |


#### Resolved intent schema

ProfessionalDutyChangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T114](../reactors/professional-duty-change-reactor.md#t114) | logical prerequisite | FirmProfessionalDutyDesignation | SettleFirmStaffProfessionalDutyUse | FirmStaffRecord |
| [A000](../reactors/unassigned-architecture-rows.md#a000) | emitted semantic fact | FirmProfessionalDutyDesignation | RejectProfessionalDutyChange | FirmProfessionalDutyDesignation |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
