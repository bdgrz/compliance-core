# ProfessionalAssertionIdentityClaim events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/professional-assertion-identity-claim.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ProfessionalAssertionIdentityClaimed | business | bdgrz.professional-assertion-identity-claim.professional-assertion-identity-claimed | 1 | 8 | The permanent claim of one signed assertion identity by one professional-duty change. The complete verified tuple is the frozen intent the fact points to; the claim is never released. |

## Event details

### ProfessionalAssertionIdentityClaimed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProfessionalAssertionIdentityClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProfessionalAssertionIdentityClaimed |
| Adopted wire | `{"discriminator":"bdgrz.professional-assertion-identity-claim.professional-assertion-identity-claimed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ProfessionalAssertionIdentityClaimed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The permanent claim of one signed assertion identity by one professional-duty change. The complete verified tuple is the frozen intent the fact points to; the claim is never released. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ProfessionalAssertionIdentityClaimed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProfessionalAssertionIdentityClaimed |
| Schema discriminator | bdgrz.professional-assertion-identity-claim.professional-assertion-identity-claimed |
| Schema discriminatorVersion | 1 |
| Schema owner | ProfessionalAssertionIdentityClaim |
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
| [T000](../reactors/professional-duty-change-reactor.md#t000) | emitted semantic fact | FirmProfessionalDutyDesignation | ClaimProfessionalAssertionIdentity | ProfessionalAssertionIdentityClaim |
| [T001](../reactors/professional-duty-change-reactor.md#t001) | logical prerequisite | ProfessionalAssertionIdentityClaim | ApplyProfessionalDutyChange | FirmProfessionalDutyDesignation |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
