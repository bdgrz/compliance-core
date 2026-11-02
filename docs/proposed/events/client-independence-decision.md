# ClientIndependenceDecision events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/client-independence-decision.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ClientIndependenceEvaluated | business | bdgrz.independence.evaluated | 1 | 8 | The immutable historical preview of one admitted evaluation, materialized from the frozen intent only. The complete IndependenceEvaluationView is immutable content; the preview never grants production acceptance. The evaluation identity is OperationId. |

## Event details

### ClientIndependenceEvaluated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ClientIndependenceEvaluated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientIndependenceEvaluated |
| Adopted wire | `{"discriminator":"bdgrz.independence.evaluated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ClientIndependenceEvaluated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The immutable historical preview of one admitted evaluation, materialized from the frozen intent only. The complete IndependenceEvaluationView is immutable content; the preview never grants production acceptance. The evaluation identity is OperationId. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ClientIndependenceEvaluated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ClientIndependenceEvaluated |
| Schema discriminator | bdgrz.independence.evaluated |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientIndependenceDecision |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/ClientIndependenceEvaluated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured IndependenceEvaluationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| EvaluationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ClientIndependenceEvaluated.EvaluationContent.v1

| Property | Value |
| --- | --- |
| field | EvaluationContent |
| schema | ClientIndependenceEvaluated.EvaluationContent.v1 |
| contentType | IndependenceEvaluationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: client-independence-preview-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | client-independence-preview-intent.v1 |
| recordType | ClientIndependencePreviewIntent |
| rule | Freeze complete exact current service epoch and historical evaluation inputs at client admission. Child calculation reads only this snapshot with the selected evaluator version. Result is a historical preview; ProductionAcceptanceBlocked is always true. Infrastructure/missing coverage is transient; proven missing business facts become explicit findings. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EvaluationId | Uuid | true |
| ClientTenantId | Uuid | true |
| ServiceEpoch | ulong | true |
| RuleSetVersion | long | true |
| ExaminationPeriodStart | DateOnly | true |
| CompleteServices | ImmutableSelectionManifest | true |
| RuleContent | ImmutableContentRef | true |
| HistoricalPracticeFacts | ImmutableSelectionManifest | true |
| SourceCuts | ImmutableSelectionManifest | true |
| RuleVersionDigest | string | true |

##### Variant: ClientIndependencePreviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EvaluationId | Uuid | true |
| ClientTenantId | Uuid | true |
| ServiceEpoch | ulong | true |
| RuleSetVersion | long | true |
| ExaminationPeriodStart | DateOnly | true |
| CompleteServices | ImmutableSelectionManifest | true |
| RuleContent | ImmutableContentRef | true |
| HistoricalPracticeFacts | ImmutableSelectionManifest | true |
| SourceCuts | ImmutableSelectionManifest | true |
| RuleVersionDigest | string | true |


#### Resolved intent schema

ClientIndependencePreviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T106](../reactors/client-independence-preview-reactor.md#t106) | emitted semantic fact | ClientServiceIndependence | MaterializeClientIndependencePreview | ClientIndependenceDecision |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
