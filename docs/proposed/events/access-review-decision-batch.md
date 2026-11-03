# AccessReviewDecisionBatch events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-review-decision-batch.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AccessReviewDecisionBatchAccepted | AccessReviewDecisionBatchAccepted | business | bdgrz.access-review-decision-batch.access-review-decision-batch-accepted | 1 | 8 | The batch owner materialized the admitted batch at logical revision zero from the exact admitted BulkAccessDecisionIntent. Nothing is reconstructed from current campaign or item state. |
| AccessReviewDecisionBatchCanceled | AccessReviewDecisionBatchCanceled | business | bdgrz.access-review-decision-batch.access-review-decision-batch-canceled | 1 | 8 | Every selected item was fenced and the batch is canceled; no decision of it is ever published. SourceProofs is the complete set of BulkItemFenceProof. |
| AccessReviewDecisionBatchCancellationAccepted | AccessReviewDecisionBatchCancellationAccepted | business | bdgrz.access-review-decision-batch.access-review-decision-batch-cancellation-accepted | 1 | 8 | The batch accepted a cancellation before any irreversible publication. From this fact every selected item is fenced against delayed holds and applies before any hold is released. SourceProofs is the failure proof. |
| AccessReviewDecisionBatchPublished | AccessReviewDecisionBatchPublished | business | bdgrz.access-review-decision-batch.access-review-decision-batch-published | 1 | 8 | Every selected item held its provisional decision and the batch published all of them as one fact. SourceProofs is the complete set of BulkItemDecisionProof (exact item event and position); OperationResult is the terminal BulkAccessDecisionResult. |

## Event details

### AccessReviewDecisionBatchAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewDecisionBatchAccepted |
| Selected source event name | AccessReviewDecisionBatchAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewDecisionBatchAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewDecisionBatchAccepted |
| Adopted wire | `{"discriminator":"bdgrz.access-review-decision-batch.access-review-decision-batch-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The batch owner materialized the admitted batch at logical revision zero from the exact admitted BulkAccessDecisionIntent. Nothing is reconstructed from current campaign or item state. |
| Selected source meaning | The batch owner materialized the admitted batch at logical revision zero from the exact admitted BulkAccessDecisionIntent. Nothing is reconstructed from current campaign or item state. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewDecisionBatchAccepted |
| Schema discriminator | bdgrz.access-review-decision-batch.access-review-decision-batch-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewDecisionBatch |
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

#### Content schema: bulk-access-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | bulk-access-decision-intent.v1 |
| recordType | BulkAccessDecisionIntent |
| rule | Admit on campaign first. Item list is duplicate-free and complete; preserve exact shared rationale. Campaign closure drains this admitted batch before acquiring item completion uses. Provisional decisions are hidden until one batch publication. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |

##### Variant: BulkAccessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |


#### Resolved intent schema

BulkAccessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T009](../reactors/access-review-bulk-admission-reactor.md#t009) | logical prerequisite | AccessReviewDecisionBatch | ReserveAccessReviewItemDecision | AccessReviewItem |
| [T010](../reactors/access-review-bulk-admission-reactor.md#t010) | logical prerequisite | AccessReviewDecisionBatch | ApplyAccessReviewItemDecision | AccessReviewItem |
| [T011](../reactors/access-review-bulk-admission-reactor.md#t011) | logical prerequisite | AccessReviewDecisionBatch | PublishAccessReviewDecisionBatch | AccessReviewDecisionBatch |
| [T086](../reactors/access-review-bulk-admission-reactor.md#t086) | emitted semantic fact | AccessReviewCampaign | RecordAccessReviewDecisionBatch | AccessReviewDecisionBatch |

### AccessReviewDecisionBatchCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewDecisionBatchCanceled |
| Selected source event name | AccessReviewDecisionBatchCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewDecisionBatchCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewDecisionBatchCanceled |
| Adopted wire | `{"discriminator":"bdgrz.access-review-decision-batch.access-review-decision-batch-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Every selected item was fenced and the batch is canceled; no decision of it is ever published. SourceProofs is the complete set of BulkItemFenceProof. |
| Selected source meaning | Every selected item was fenced and the batch is canceled; no decision of it is ever published. SourceProofs is the complete set of BulkItemFenceProof. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewDecisionBatchCanceled |
| Schema discriminator | bdgrz.access-review-decision-batch.access-review-decision-batch-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewDecisionBatch |
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

#### Content schema: bulk-access-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | bulk-access-decision-intent.v1 |
| recordType | BulkAccessDecisionIntent |
| rule | Admit on campaign first. Item list is duplicate-free and complete; preserve exact shared rationale. Campaign closure drains this admitted batch before acquiring item completion uses. Provisional decisions are hidden until one batch publication. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |

##### Variant: BulkAccessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |


#### Resolved intent schema

BulkAccessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T079](../reactors/access-review-bulk-cancellation-completion-reactor.md#t079) | emitted semantic fact | AccessReviewDecisionBatch | CompleteAccessReviewDecisionBatchCancellation | AccessReviewDecisionBatch |
| [T080](../reactors/access-review-bulk-admission-reactor.md#t080) | physical root, logical prerequisite | AccessReviewDecisionBatch | CloseAccessReviewItemDecisionAuthorization | AccessReviewItem |

### AccessReviewDecisionBatchCancellationAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewDecisionBatchCancellationAccepted |
| Selected source event name | AccessReviewDecisionBatchCancellationAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewDecisionBatchCancellationAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewDecisionBatchCancellationAccepted |
| Adopted wire | `{"discriminator":"bdgrz.access-review-decision-batch.access-review-decision-batch-cancellation-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchCancellationAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The batch accepted a cancellation before any irreversible publication. From this fact every selected item is fenced against delayed holds and applies before any hold is released. SourceProofs is the failure proof. |
| Selected source meaning | The batch accepted a cancellation before any irreversible publication. From this fact every selected item is fenced against delayed holds and applies before any hold is released. SourceProofs is the failure proof. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchCancellationAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewDecisionBatchCancellationAccepted |
| Schema discriminator | bdgrz.access-review-decision-batch.access-review-decision-batch-cancellation-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewDecisionBatch |
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

#### Content schema: bulk-access-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | bulk-access-decision-intent.v1 |
| recordType | BulkAccessDecisionIntent |
| rule | Admit on campaign first. Item list is duplicate-free and complete; preserve exact shared rationale. Campaign closure drains this admitted batch before acquiring item completion uses. Provisional decisions are hidden until one batch publication. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |

##### Variant: BulkAccessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |


#### Resolved intent schema

BulkAccessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T012](../reactors/access-review-bulk-cancellation-reactor.md#t012) | physical root, logical prerequisite | AccessReviewDecisionBatch | CancelAccessReviewItemDecision | AccessReviewItem |
| [T079](../reactors/access-review-bulk-cancellation-completion-reactor.md#t079) | physical root, logical prerequisite | AccessReviewDecisionBatch | CompleteAccessReviewDecisionBatchCancellation | AccessReviewDecisionBatch |
| [A005](../reactors/unassigned-architecture-rows.md#a005) | emitted semantic fact | AccessReviewCampaign | CancelAccessReviewDecisionBatch | AccessReviewDecisionBatch |

### AccessReviewDecisionBatchPublished

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewDecisionBatchPublished |
| Selected source event name | AccessReviewDecisionBatchPublished |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewDecisionBatchPublished |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewDecisionBatchPublished |
| Adopted wire | `{"discriminator":"bdgrz.access-review-decision-batch.access-review-decision-batch-published","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchPublished.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Every selected item held its provisional decision and the batch published all of them as one fact. SourceProofs is the complete set of BulkItemDecisionProof (exact item event and position); OperationResult is the terminal BulkAccessDecisionResult. |
| Selected source meaning | Every selected item held its provisional decision and the batch published all of them as one fact. SourceProofs is the complete set of BulkItemDecisionProof (exact item event and position); OperationResult is the terminal BulkAccessDecisionResult. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchPublished.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewDecisionBatchPublished |
| Schema discriminator | bdgrz.access-review-decision-batch.access-review-decision-batch-published |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewDecisionBatch |
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

#### Content schema: bulk-access-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | bulk-access-decision-intent.v1 |
| recordType | BulkAccessDecisionIntent |
| rule | Admit on campaign first. Item list is duplicate-free and complete; preserve exact shared rationale. Campaign closure drains this admitted batch before acquiring item completion uses. Provisional decisions are hidden until one batch publication. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |

##### Variant: BulkAccessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| DecisionBatchId | Uuid | true |
| ExpectedCampaignRevision | ulong | true |
| PreviewToken | string | true |
| Items | ImmutableSelectionManifest | true |
| Decision | string | true |
| Rationale | string | true |


#### Resolved intent schema

BulkAccessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T011](../reactors/access-review-bulk-admission-reactor.md#t011) | emitted semantic fact | AccessReviewDecisionBatch | PublishAccessReviewDecisionBatch | AccessReviewDecisionBatch |
| [T080](../reactors/access-review-bulk-admission-reactor.md#t080) | physical root, logical prerequisite | AccessReviewDecisionBatch | CloseAccessReviewItemDecisionAuthorization | AccessReviewItem |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
