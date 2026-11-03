# AccessPrincipalClassification events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-principal-classification.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AccessPrincipalClassified | AccessPrincipalClassified | business | bdgrz.access_population.principal_classified | 2 | 7 | An explicit classification decision for one exact accepted principal; earlier decisions remain in history. The decision number is the position of this fact in the principal's own stream. DecisionContent is the complete AccessPrincipalClassificationView. |

## Event details

### AccessPrincipalClassified

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessPrincipalClassified |
| Selected source event name | AccessPrincipalClassified |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessPrincipalClassified |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessPrincipalClassified |
| Adopted wire | `{"discriminator":"bdgrz.access_population.principal_classified","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessPrincipalClassified.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_population.principal_classified","version":1}` |
| Target meaning | An explicit classification decision for one exact accepted principal; earlier decisions remain in history. The decision number is the position of this fact in the principal's own stream. DecisionContent is the complete AccessPrincipalClassificationView. |
| Selected source meaning | An explicit classification decision for one exact accepted principal; earlier decisions remain in history. The decision number is the position of this fact in the principal's own stream. DecisionContent is the complete AccessPrincipalClassificationView. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessPrincipalClassified.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessPrincipalClassified |
| Schema discriminator | bdgrz.access_population.principal_classified |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessPrincipalClassification |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessPrincipalClassified.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessPrincipalClassificationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| ProviderSubjectId | string | true |
| DecisionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessPrincipalClassified.DecisionContent.v1

| Property | Value |
| --- | --- |
| field | DecisionContent |
| schema | AccessPrincipalClassified.DecisionContent.v1 |
| contentType | AccessPrincipalClassificationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AccessPrincipalClassified** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
