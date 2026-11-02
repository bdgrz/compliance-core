# ProviderAssuranceReview events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/provider-assurance-review.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ProviderReviewCanceled | business | bdgrz.provider-assurance-review.provider-review-canceled | 1 | 8 | A pending provider assurance review was canceled before being recorded because its source was proven permanently mismatched, so the review can no longer be recorded. |
| ProviderReviewRecorded | business | bdgrz.provider.review.recorded | 1 | 14 | The admitted review was recorded as an immutable fact once its report use was held. The complete ProviderReviewContent is immutable content; the provider revision and materiality are the observed historical facts of the recording. |
| ProviderReviewRequested | business | bdgrz.provider-assurance-review.provider-review-requested | 1 | 8 | A provider assurance review was admitted for one provider against its frozen review intent, permitting the review result to be recorded. |

## Event details

### ProviderReviewCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderReviewCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderReviewCanceled |
| Adopted wire | `{"discriminator":"bdgrz.provider-assurance-review.provider-review-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderReviewCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A pending provider assurance review was canceled before being recorded because its source was proven permanently mismatched, so the review can no longer be recorded. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Providers/Events/ProviderReviewCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderReviewCanceled |
| Schema discriminator | bdgrz.provider-assurance-review.provider-review-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReview |
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

#### Content schema: provider-review-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | provider-review-intent.v1 |
| recordType | ProviderReviewIntent |
| rule | AuthoredReview is normalized RecordProviderReview with exact evidence/report/period/currency/cadence/rationale/actor. Source-current rules are held until irreversible native review or local cancellation fence. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| ReviewedReport | SourceFactLocator | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| ReviewedAt | DateTimeOffset | true |

##### Variant: ProviderReviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| ReviewedReport | SourceFactLocator | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| ReviewedAt | DateTimeOffset | true |


#### Resolved intent schema

ProviderReviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T070](../reactors/provider-review-reactor.md#t070) | logical prerequisite | ProviderAssuranceReview | SettleProviderAssuranceReportProviderReviewUse | ProviderAssuranceReport |
| [A011](../reactors/unassigned-architecture-rows.md#a011) | emitted semantic fact | ProviderAssuranceReview | AbortProviderReview | ProviderAssuranceReview |

### ProviderReviewRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderReviewRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderReviewRecorded |
| Adopted wire | `{"discriminator":"bdgrz.provider.review.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderReviewRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The admitted review was recorded as an immutable fact once its report use was held. The complete ProviderReviewContent is immutable content; the provider revision and materiality are the observed historical facts of the recording. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderReviewRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderReviewRecorded |
| Schema discriminator | bdgrz.provider.review.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReview |
| Schema existingContract | src/Compliance.Common/Features/Providers/ProviderReviewRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured ProviderReviewContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| ContentContent | ImmutableContentRef | true |
| ProviderRevision | long | true |
| ProviderMateriality | string? | true |
| AssuranceReportRevision | long? | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ProviderReviewRecorded.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | ProviderReviewRecorded.ContentContent.v1 |
| contentType | ProviderReviewContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: provider-review-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | provider-review-intent.v1 |
| recordType | ProviderReviewIntent |
| rule | AuthoredReview is normalized RecordProviderReview with exact evidence/report/period/currency/cadence/rationale/actor. Source-current rules are held until irreversible native review or local cancellation fence. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| ReviewedReport | SourceFactLocator | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| ReviewedAt | DateTimeOffset | true |

##### Variant: ProviderReviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| ReviewedReport | SourceFactLocator | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| ReviewedAt | DateTimeOffset | true |


#### Resolved intent schema

ProviderReviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T070](../reactors/provider-review-reactor.md#t070) | logical prerequisite | ProviderAssuranceReview | SettleProviderAssuranceReportProviderReviewUse | ProviderAssuranceReport |
| [T077](../reactors/provider-review-reactor.md#t077) | emitted semantic fact | ProviderAssuranceReview | CompleteProviderAssuranceReview | ProviderAssuranceReview |

### ProviderReviewRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderReviewRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderReviewRequested |
| Adopted wire | `{"discriminator":"bdgrz.provider-assurance-review.provider-review-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderReviewRequested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A provider assurance review was admitted for one provider against its frozen review intent, permitting the review result to be recorded. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Providers/Events/ProviderReviewRequested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderReviewRequested |
| Schema discriminator | bdgrz.provider-assurance-review.provider-review-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReview |
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

#### Content schema: provider-review-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | provider-review-intent.v1 |
| recordType | ProviderReviewIntent |
| rule | AuthoredReview is normalized RecordProviderReview with exact evidence/report/period/currency/cadence/rationale/actor. Source-current rules are held until irreversible native review or local cancellation fence. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| ReviewedReport | SourceFactLocator | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| ReviewedAt | DateTimeOffset | true |

##### Variant: ProviderReviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ReviewId | Uuid | true |
| ProviderId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| ReviewedReport | SourceFactLocator | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| ReviewedAt | DateTimeOffset | true |


#### Resolved intent schema

ProviderReviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T069](../reactors/provider-review-reactor.md#t069) | physical root, logical prerequisite | ProviderAssuranceReview | AuthorizeProviderAssuranceReportProviderReviewUse | ProviderAssuranceReport |
| [T070](../reactors/provider-review-reactor.md#t070) | physical root | ProviderAssuranceReview | SettleProviderAssuranceReportProviderReviewUse | ProviderAssuranceReport |
| [T077](../reactors/provider-review-reactor.md#t077) | physical root, logical prerequisite | ProviderAssuranceReview | CompleteProviderAssuranceReview | ProviderAssuranceReview |
| [A011](../reactors/unassigned-architecture-rows.md#a011) | physical root, logical prerequisite | ProviderAssuranceReview | AbortProviderReview | ProviderAssuranceReview |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
