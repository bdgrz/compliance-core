# ProviderRiskAcceptance events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/provider-risk-acceptance.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ProviderCoverageGapRiskAcceptanceLinked | ProviderCoverageGapRiskAcceptanceLinked | business | bdgrz.provider.coverage_gap.risk_acceptance_linked | 1 | 10 | The immutable accepted-risk link materialized from its admission. It belongs only to the link id, never changes the gap, and keeps the admission revision and instant even if the gap later closes or the acceptance expires. AcceptanceContent is the ProviderCoverageGapRiskAcceptanceView. |

## Event details

### ProviderCoverageGapRiskAcceptanceLinked

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProviderCoverageGapRiskAcceptanceLinked |
| Selected source event name | ProviderCoverageGapRiskAcceptanceLinked |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProviderCoverageGapRiskAcceptanceLinked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderCoverageGapRiskAcceptanceLinked |
| Adopted wire | `{"discriminator":"bdgrz.provider.coverage_gap.risk_acceptance_linked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRiskAcceptanceLinked.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The immutable accepted-risk link materialized from its admission. It belongs only to the link id, never changes the gap, and keeps the admission revision and instant even if the gap later closes or the acceptance expires. AcceptanceContent is the ProviderCoverageGapRiskAcceptanceView. |
| Selected source meaning | The immutable accepted-risk link materialized from its admission. It belongs only to the link id, never changes the gap, and keeps the admission revision and instant even if the gap later closes or the acceptance expires. AcceptanceContent is the ProviderCoverageGapRiskAcceptanceView. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderCoverageGapRiskAcceptanceLinked.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderCoverageGapRiskAcceptanceLinked |
| Schema discriminator | bdgrz.provider.coverage_gap.risk_acceptance_linked |
| Schema discriminatorVersion | 1 |
| Schema owner | ProviderRiskAcceptance |
| Schema existingContract | src/Compliance.Common/Features/Providers/ProviderCoverageGapRiskAcceptanceLinked.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured ProviderCoverageGapRiskAcceptanceView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
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
| AcceptanceContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ProviderCoverageGapRiskAcceptanceLinked.AcceptanceContent.v1

| Property | Value |
| --- | --- |
| field | AcceptanceContent |
| schema | ProviderCoverageGapRiskAcceptanceLinked.AcceptanceContent.v1 |
| contentType | ProviderCoverageGapRiskAcceptanceView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

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
| [T088](../reactors/keep-risk-acceptance-with-provider-gap-reactor.md#t088) | emitted semantic fact | ProviderAssuranceGap | LinkProviderRiskAcceptanceToGap | ProviderRiskAcceptance |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
