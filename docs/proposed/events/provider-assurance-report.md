# ProviderAssuranceReport events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/provider-assurance-report.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AssuranceReportRecorded | AssuranceReportRecorded | business | bdgrz.provider.assurance_report.recorded | 1 | 11 | An assurance report was recorded as the first revision of its own owner. The complete AssuranceReportContent is immutable content; RequestId is the report id, the logical identity of the creation. |
| AssuranceReportRevised | AssuranceReportRevised | business | bdgrz.provider.assurance_report.revised | 1 | 12 | An assurance report was revised at the expected revision of its own owner; the provider cannot change. The complete revised AssuranceReportContent is immutable content and RequestId is the command id of the revision. |
| ProviderAssuranceReportProviderReviewAuthorized | ProviderAssuranceReportProviderReviewUseAuthorized | reservation | bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-authorized | 1 | 8 | The exact assurance report revision is held for one provider review. The use does not advance the public report revision; Revision is the held revision and the report refuses revision while held. |
| ProviderAssuranceReportProviderReviewAuthorizationClosed | ProviderAssuranceReportProviderReviewUseSettled | reservation | bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-settled | 1 | 8 | The held report use ended after the recorded review or after the cancellation fence. With no earlier grant it fences the key so a delayed acquisition cannot recreate the hold. |

## Event details

### AssuranceReportRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | AssuranceReportRecorded |
| Selected source event name | AssuranceReportRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AssuranceReportRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AssuranceReportRecorded |
| Adopted wire | `{"discriminator":"bdgrz.provider.assurance_report.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/AssuranceReportRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An assurance report was recorded as the first revision of its own owner. The complete AssuranceReportContent is immutable content; RequestId is the report id, the logical identity of the creation. |
| Selected source meaning | An assurance report was recorded as the first revision of its own owner. The complete AssuranceReportContent is immutable content; RequestId is the report id, the logical identity of the creation. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/AssuranceReportRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AssuranceReportRecorded |
| Schema discriminator | bdgrz.provider.assurance_report.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReport |
| Schema existingContract | src/Compliance.Common/Features/Providers/AssuranceReportRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AssuranceReportContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ReportId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| ContentContent | ImmutableContentRef | true |
| ProviderRevision | long | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AssuranceReportRecorded.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | AssuranceReportRecorded.ContentContent.v1 |
| contentType | AssuranceReportContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AssuranceReportRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | AssuranceReportRevised |
| Selected source event name | AssuranceReportRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AssuranceReportRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AssuranceReportRevised |
| Adopted wire | `{"discriminator":"bdgrz.provider.assurance_report.revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/AssuranceReportRevised.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An assurance report was revised at the expected revision of its own owner; the provider cannot change. The complete revised AssuranceReportContent is immutable content and RequestId is the command id of the revision. |
| Selected source meaning | An assurance report was revised at the expected revision of its own owner; the provider cannot change. The complete revised AssuranceReportContent is immutable content and RequestId is the command id of the revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/AssuranceReportRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AssuranceReportRevised |
| Schema discriminator | bdgrz.provider.assurance_report.revised |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReport |
| Schema existingContract | src/Compliance.Common/Features/Providers/AssuranceReportRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AssuranceReportContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ReportId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| Revision | long | true |
| ContentContent | ImmutableContentRef | true |
| ProviderRevision | long | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AssuranceReportRevised.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | AssuranceReportRevised.ContentContent.v1 |
| contentType | AssuranceReportContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProviderAssuranceReportProviderReviewAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProviderAssuranceReportProviderReviewAuthorized |
| Selected source event name | ProviderAssuranceReportProviderReviewUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:ProviderAssuranceReportProviderReviewUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderAssuranceReportProviderReviewUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderAssuranceReportProviderReviewUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The exact assurance report revision is held for one provider review. The use does not advance the public report revision; Revision is the held revision and the report refuses revision while held. |
| Selected source meaning | The exact assurance report revision is held for one provider review. The use does not advance the public report revision; Revision is the held revision and the report refuses revision while held. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderAssuranceReportProviderReviewUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderAssuranceReportProviderReviewUseAuthorized |
| Schema discriminator | bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReport |
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
| [T069](../reactors/record-provider-assurance-report-review-reactor.md#t069) | emitted semantic fact | ProviderAssuranceReview | AuthorizeProviderAssuranceReportProviderReview | ProviderAssuranceReport |

### ProviderAssuranceReportProviderReviewAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProviderAssuranceReportProviderReviewAuthorizationClosed |
| Selected source event name | ProviderAssuranceReportProviderReviewUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:ProviderAssuranceReportProviderReviewUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderAssuranceReportProviderReviewUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderAssuranceReportProviderReviewUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The held report use ended after the recorded review or after the cancellation fence. With no earlier grant it fences the key so a delayed acquisition cannot recreate the hold. |
| Selected source meaning | The held report use ended after the recorded review or after the cancellation fence. With no earlier grant it fences the key so a delayed acquisition cannot recreate the hold. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderAssuranceReportProviderReviewUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderAssuranceReportProviderReviewUseSettled |
| Schema discriminator | bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceReport |
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
| [T070](../reactors/record-provider-assurance-report-review-reactor.md#t070) | emitted semantic fact | ProviderAssuranceReview | CloseProviderAssuranceReportProviderReviewAuthorization | ProviderAssuranceReport |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
