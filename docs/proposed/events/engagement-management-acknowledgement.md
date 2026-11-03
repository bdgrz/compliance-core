# EngagementManagementAcknowledgement events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/engagement-management-acknowledgement.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| EngagementManagementAcknowledged | EngagementManagementAcknowledged | business | bdgrz.service-engagement.management-acknowledged | 1 | 8 | The immutable personal management acknowledgement of one engagement, materialized from the authorized personal decision. The complete EngagementManagementAcknowledgementView is immutable content; the acknowledgement identity is OperationId. |

## Event details

### EngagementManagementAcknowledged

| Canonical event detail | Value |
| --- | --- |
| Target event name | EngagementManagementAcknowledged |
| Selected source event name | EngagementManagementAcknowledged |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EngagementManagementAcknowledged |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementManagementAcknowledged |
| Adopted wire | `{"discriminator":"bdgrz.service-engagement.management-acknowledged","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementManagementAcknowledged.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The immutable personal management acknowledgement of one engagement, materialized from the authorized personal decision. The complete EngagementManagementAcknowledgementView is immutable content; the acknowledgement identity is OperationId. |
| Selected source meaning | The immutable personal management acknowledgement of one engagement, materialized from the authorized personal decision. The complete EngagementManagementAcknowledgementView is immutable content; the acknowledgement identity is OperationId. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementManagementAcknowledged.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementManagementAcknowledged |
| Schema discriminator | bdgrz.service-engagement.management-acknowledged |
| Schema discriminatorVersion | 1 |
| Schema owner | EngagementManagementAcknowledgement |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/EngagementManagementAcknowledged.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured EngagementManagementAcknowledgementView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| AcknowledgementContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: EngagementManagementAcknowledged.AcknowledgementContent.v1

| Property | Value |
| --- | --- |
| field | AcknowledgementContent |
| schema | EngagementManagementAcknowledged.AcknowledgementContent.v1 |
| contentType | EngagementManagementAcknowledgementView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: engagement-personal-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-personal-decision-intent.v1 |
| recordType | EngagementPersonalDecisionIntent |
| rule | DecisionId is AcknowledgementId or EvaluationId. Admission pins exact engagement content and epoch. Management content contains complete service record IDs and statement; partner content contains exact rule/digest/history/rationale and authoritative personal duty requirements. Authorize only after all required uses; cancel only before authorization. Child materialization consumes authorization, records frozen original actor and decision instant, and cannot overwrite a later personal decision pointer. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: EngagementPersonalDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DecisionId | Uuid | true |
| Kind | EngagementPersonalDecisionKind | true |
| EngagementId | Uuid | true |
| ExpectedEngagementRevision | ulong | true |
| ServiceEpoch | ulong | true |
| CompleteServiceManifest | ImmutableSelectionManifest | true |
| AuthoredContent | EngagementPersonalDecisionContent | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

EngagementPersonalDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T108](../reactors/engagement-personal-decision-reactor.md#t108) | emitted semantic fact | ServiceEngagementIndependence | RecordEngagementManagementAcknowledgement | EngagementManagementAcknowledgement |
| [T110](../reactors/engagement-personal-decision-reactor.md#t110) | logical prerequisite | EngagementManagementAcknowledgement\|PartnerIndependenceDecision | CloseEngagementPersonalDecision | ServiceEngagementIndependence |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
