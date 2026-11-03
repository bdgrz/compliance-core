# IndependenceRuleCatalog events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/independence-rule-catalog.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| IndependenceRuleCatalogEngagementAuthorized | IndependenceRuleCatalogEngagementUseAuthorized | reservation | bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-authorized | 1 | 8 | The exact revised independence rule catalog version a partner evaluation relies on was held for the engagement; it does not publish the catalog and blocks revision until the use settles. |
| IndependenceRuleCatalogEngagementAuthorizationClosed | IndependenceRuleCatalogEngagementUseSettled | reservation | bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-settled | 1 | 8 | The use of the draft rule catalog ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| IndependenceRulesRevised | IndependenceRulesRevised | business | bdgrz.independence.rules.revised | 2 | [Placeholder: schema absent] | The independence rule catalog was revised with new rule content, becoming the next draft version of the catalog. |

## Event details

### IndependenceRuleCatalogEngagementAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | IndependenceRuleCatalogEngagementAuthorized |
| Selected source event name | IndependenceRuleCatalogEngagementUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:IndependenceRuleCatalogEngagementUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IndependenceRuleCatalogEngagementUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleCatalogEngagementUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The exact revised independence rule catalog version a partner evaluation relies on was held for the engagement; it does not publish the catalog and blocks revision until the use settles. |
| Selected source meaning | The exact revised independence rule catalog version a partner evaluation relies on was held for the engagement; it does not publish the catalog and blocks revision until the use settles. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleCatalogEngagementUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IndependenceRuleCatalogEngagementUseAuthorized |
| Schema discriminator | bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | IndependenceRuleCatalog |
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
| [T093](../reactors/engagement-acceptance-reactor.md#t093) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeIndependenceRuleCatalogEngagement | IndependenceRuleCatalog |

### IndependenceRuleCatalogEngagementAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | IndependenceRuleCatalogEngagementAuthorizationClosed |
| Selected source event name | IndependenceRuleCatalogEngagementUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:IndependenceRuleCatalogEngagementUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IndependenceRuleCatalogEngagementUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleCatalogEngagementUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The use of the draft rule catalog ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Selected source meaning | The use of the draft rule catalog ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/IndependenceRuleCatalogEngagementUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | IndependenceRuleCatalogEngagementUseSettled |
| Schema discriminator | bdgrz.independence-rule-catalog.independence-rule-catalog-engagement-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | IndependenceRuleCatalog |
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
| [T094](../reactors/engagement-acceptance-reactor.md#t094) | emitted semantic fact | ServiceEngagementIndependence | CloseIndependenceRuleCatalogEngagementAuthorization | IndependenceRuleCatalog |

### IndependenceRulesRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | IndependenceRulesRevised |
| Selected source event name | IndependenceRulesRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IndependenceRulesRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IndependenceRulesRevised |
| Adopted wire | `{"discriminator":"bdgrz.independence.rules.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/IndependenceRulesRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The independence rule catalog was revised with new rule content, becoming the next draft version of the catalog. |
| Selected source meaning | The independence rule catalog was revised with new rule content, becoming the next draft version of the catalog. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/IndependenceRulesRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
