# IndependenceRuleRatificationCatalog events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/independence-rule-ratification-catalog.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| IndependenceRuleRatificationCatalogEngagementUseAuthorized | reservation | bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-authorized | 1 | 8 | The exact ratification of an independence rule version that a partner evaluation relies on was held for the engagement; the hold blocks changes to that ratification until the use settles. |
| IndependenceRuleRatificationCatalogEngagementUseSettled | reservation | bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-settled | 1 | 8 | The use of the ratification catalog ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| IndependenceRuleRatificationRecorded | business | bdgrz.independence.rules.ratification-recorded | 2 | [Placeholder: schema absent] | A named rule ratifier personally ratified a version of the independence rules, making that version the ratified catalog. |

## Event details

### IndependenceRuleRatificationCatalogEngagementUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:IndependenceRuleRatificationCatalogEngagementUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IndependenceRuleRatificationCatalogEngagementUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleRatificationCatalogEngagementUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The exact ratification of an independence rule version that a partner evaluation relies on was held for the engagement; the hold blocks changes to that ratification until the use settles. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleRatificationCatalogEngagementUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IndependenceRuleRatificationCatalogEngagementUseAuthorized |
| Schema discriminator | bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | IndependenceRuleRatificationCatalog |
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
| [T095](../reactors/engagement-acceptance-reactor.md#t095) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |

### IndependenceRuleRatificationCatalogEngagementUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:IndependenceRuleRatificationCatalogEngagementUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IndependenceRuleRatificationCatalogEngagementUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleRatificationCatalogEngagementUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The use of the ratification catalog ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleRatificationCatalogEngagementUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IndependenceRuleRatificationCatalogEngagementUseSettled |
| Schema discriminator | bdgrz.independence-rule-ratification-catalog.independence-rule-ratification-catalog-engagement-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | IndependenceRuleRatificationCatalog |
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
| [T096](../reactors/engagement-acceptance-reactor.md#t096) | emitted semantic fact | ServiceEngagementIndependence | SettleIndependenceRuleRatificationCatalogEngagementUse | IndependenceRuleRatificationCatalog |

### IndependenceRuleRatificationRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:IndependenceRuleRatificationRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IndependenceRuleRatificationRecorded |
| Adopted wire | `{"discriminator":"bdgrz.independence.rules.ratification-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleRatificationRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A named rule ratifier personally ratified a version of the independence rules, making that version the ratified catalog. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleRatificationRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
