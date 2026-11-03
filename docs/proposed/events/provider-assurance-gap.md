# ProviderAssuranceGap events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/provider-assurance-gap.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ProviderCoverageGapClosed | ProviderCoverageGapClosed | business | bdgrz.provider.coverage_gap.closed | 1 | 11 | The open provider coverage gap was closed once, against a newer or different source. The complete ProviderCoverageGapClosureContent is immutable content. A closure never erases an already admitted risk link. |
| ProviderCoverageGapRecorded | ProviderCoverageGapRecorded | business | bdgrz.provider.coverage_gap.recorded | 1 | 11 | A provider coverage gap was recorded as the first revision of its own owner. The complete ProviderCoverageGapContent is immutable content; RequestId is the gap id. |
| ProviderCoverageGapRiskAcceptanceAdmitted | ProviderCoverageGapRiskAcceptanceAdmitted | business | bdgrz.provider-assurance-gap.provider-coverage-gap-risk-acceptance-admitted | 1 | 8 | A risk acceptance link was admitted on the open gap at its expected revision. Revision is the gap revision this admission produced; OperationProvenance.Episode carries the admitted RiskId:AcceptanceId pair, which the gap serializes for uniqueness. Intent is the frozen ProviderRiskLinkIntent. |

## Event details

### ProviderCoverageGapClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProviderCoverageGapClosed |
| Selected source event name | ProviderCoverageGapClosed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProviderCoverageGapClosed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderCoverageGapClosed |
| Adopted wire | `{"discriminator":"bdgrz.provider.coverage_gap.closed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapClosed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The open provider coverage gap was closed once, against a newer or different source. The complete ProviderCoverageGapClosureContent is immutable content. A closure never erases an already admitted risk link. |
| Selected source meaning | The open provider coverage gap was closed once, against a newer or different source. The complete ProviderCoverageGapClosureContent is immutable content. A closure never erases an already admitted risk link. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapClosed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderCoverageGapClosed |
| Schema discriminator | bdgrz.provider.coverage_gap.closed |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceGap |
| Schema existingContract | src/Compliance.Common/Features/Providers/ProviderCoverageGapClosed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ProviderCoverageGapClosureContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| GapId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| Revision | long | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| ClosedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ProviderCoverageGapClosed.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | ProviderCoverageGapClosed.ContentContent.v1 |
| contentType | ProviderCoverageGapClosureContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProviderCoverageGapRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProviderCoverageGapRecorded |
| Selected source event name | ProviderCoverageGapRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProviderCoverageGapRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderCoverageGapRecorded |
| Adopted wire | `{"discriminator":"bdgrz.provider.coverage_gap.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A provider coverage gap was recorded as the first revision of its own owner. The complete ProviderCoverageGapContent is immutable content; RequestId is the gap id. |
| Selected source meaning | A provider coverage gap was recorded as the first revision of its own owner. The complete ProviderCoverageGapContent is immutable content; RequestId is the gap id. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderCoverageGapRecorded |
| Schema discriminator | bdgrz.provider.coverage_gap.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceGap |
| Schema existingContract | src/Compliance.Common/Features/Providers/ProviderCoverageGapRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ProviderCoverageGapContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| GapId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| ProviderRevision | long | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ProviderCoverageGapRecorded.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | ProviderCoverageGapRecorded.ContentContent.v1 |
| contentType | ProviderCoverageGapContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProviderCoverageGapRiskAcceptanceAdmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProviderCoverageGapRiskAcceptanceAdmitted |
| Selected source event name | ProviderCoverageGapRiskAcceptanceAdmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProviderCoverageGapRiskAcceptanceAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderCoverageGapRiskAcceptanceAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.provider-assurance-gap.provider-coverage-gap-risk-acceptance-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRiskAcceptanceAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A risk acceptance link was admitted on the open gap at its expected revision. Revision is the gap revision this admission produced; OperationProvenance.Episode carries the admitted RiskId:AcceptanceId pair, which the gap serializes for uniqueness. Intent is the frozen ProviderRiskLinkIntent. |
| Selected source meaning | A risk acceptance link was admitted on the open gap at its expected revision. Revision is the gap revision this admission produced; OperationProvenance.Episode carries the admitted RiskId:AcceptanceId pair, which the gap serializes for uniqueness. Intent is the frozen ProviderRiskLinkIntent. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRiskAcceptanceAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderCoverageGapRiskAcceptanceAdmitted |
| Schema discriminator | bdgrz.provider-assurance-gap.provider-coverage-gap-risk-acceptance-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderAssuranceGap |
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

#### Content schema: provider-risk-link-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | provider-risk-link-intent.v1 |
| recordType | ProviderRiskLinkIntent |
| rule | LinkId equals root CommandId. Gap serializes open check, exact revision and uniqueness of RiskId/AcceptanceId pair. LinkedAt is server admission instant; source acceptance active then. Later closure/expiry cannot erase this accepted historical linkage and never makes the gap closed. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| LinkId | Uuid | true |
| ProviderId | Uuid | true |
| GapId | Uuid | true |
| GapAdmissionRevision | ulong | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| AcceptanceId | Uuid | true |
| AcceptanceProof | SourceFactProof<RiskAccepted> | true |
| AcceptanceExpiresAt | DateTimeOffset | true |
| ProviderRevision | ulong | true |
| ServiceId | Uuid? | true |
| LinkedAt | DateTimeOffset | true |

##### Variant: ProviderRiskLinkIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| LinkId | Uuid | true |
| ProviderId | Uuid | true |
| GapId | Uuid | true |
| GapAdmissionRevision | ulong | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| AcceptanceId | Uuid | true |
| AcceptanceProof | SourceFactProof<RiskAccepted> | true |
| AcceptanceExpiresAt | DateTimeOffset | true |
| ProviderRevision | ulong | true |
| ServiceId | Uuid? | true |
| LinkedAt | DateTimeOffset | true |


#### Resolved intent schema

ProviderRiskLinkIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T088](../reactors/provider-risk-acceptance-link-reactor.md#t088) | physical root, logical prerequisite | ProviderAssuranceGap | LinkProviderRiskAcceptanceToGap | ProviderRiskAcceptance |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
