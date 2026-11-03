# AccessReviewCampaign events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-review-campaign.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AccessReviewCampaignCompleted | AccessReviewCampaignCompleted | business | bdgrz.access_review.campaign.completed | 2 | 10 | The campaign was completed with its final immutable snapshot, after every item was held and its exact decision, remediation and exception proof was verified. ItemCompletionProofs is the complete proof set; OperationResult is the original typed completion result. |
| AccessReviewCampaignCompletionAccepted | AccessReviewCampaignCompletionAccepted | business | bdgrz.access-review-campaign.access-review-campaign-completion-accepted | 1 | 8 | The campaign admitted the owner's completion: the exact campaign revision, the frozen ClosureIntent (the attestation, the complete drained item selection and the pinned head of every item) and the original 202 acknowledgment. The completion process holds every item from it. |
| AccessReviewCampaignCompletionCanceled | AccessReviewCampaignCompletionCanceled | business | bdgrz.access-review-campaign.access-review-campaign-completion-canceled | 1 | 8 | The pending completion of an access review campaign was canceled with a failure proof, so any delayed attempt to close that completion is refused and the campaign stays open. |
| AccessReviewCampaignLaunched | AccessReviewCampaignLaunched | business | bdgrz.access_review.campaign.launched | 2 | 16 | A campaign was launched with its frozen population, reviewers, instructions, and deadline. The reviewers and the complete item list are verified immutable content, so a campaign of any size is one bounded fact. ItemIntentManifest holds the frozen creation intent of every item in canonical item order; the launch fan-out materializes each item from it. The campaign's logical revision is 1 after launch. |
| AccessReviewDecisionBatchAdmitted | AccessReviewDecisionBatchAdmitted | business | bdgrz.access-review-campaign.access-review-decision-batch-admitted | 1 | 8 | The campaign admitted a complete bulk decision: the exact campaign revision, the frozen BulkAccessDecisionIntent (the preview token, the immutable selection with each item's frozen head and the shared decision and rationale) and the original 202 acknowledgment. The batch is created from this fact at its own revision zero, and a campaign completion drains it before it holds any item. |

## Event details

### AccessReviewCampaignCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewCampaignCompleted |
| Selected source event name | AccessReviewCampaignCompleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewCampaignCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewCampaignCompleted |
| Adopted wire | `{"discriminator":"bdgrz.access_review.campaign.completed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompleted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.campaign.completed","version":1}` |
| Target meaning | The campaign was completed with its final immutable snapshot, after every item was held and its exact decision, remediation and exception proof was verified. ItemCompletionProofs is the complete proof set; OperationResult is the original typed completion result. |
| Selected source meaning | The campaign was completed with its final immutable snapshot, after every item was held and its exact decision, remediation and exception proof was verified. ItemCompletionProofs is the complete proof set; OperationResult is the original typed completion result. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompleted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewCampaignCompleted |
| Schema discriminator | bdgrz.access_review.campaign.completed |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewCampaign |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignCompleted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured AccessReviewCampaignCompletionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Revision | long | true |
| CompletionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| CampaignGeneration | long | true |
| ItemCompletionProofs | ImmutableContentRef | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessReviewCampaignCompleted.CompletionContent.v1

| Property | Value |
| --- | --- |
| field | CompletionContent |
| schema | AccessReviewCampaignCompleted.CompletionContent.v1 |
| contentType | AccessReviewCampaignCompletionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T060](../reactors/access-review-completion-reactor.md#t060) | logical prerequisite | AccessReviewCampaign | CloseAccessReviewItemCampaignCompletionAuthorization | AccessReviewItem |
| [T073](../reactors/access-review-completion-reactor.md#t073) | emitted semantic fact | AccessReviewCampaign | CompleteAccessReviewCampaignClosure | AccessReviewCampaign |

### AccessReviewCampaignCompletionAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewCampaignCompletionAccepted |
| Selected source event name | AccessReviewCampaignCompletionAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewCampaignCompletionAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewCampaignCompletionAccepted |
| Adopted wire | `{"discriminator":"bdgrz.access-review-campaign.access-review-campaign-completion-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompletionAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The campaign admitted the owner's completion: the exact campaign revision, the frozen ClosureIntent (the attestation, the complete drained item selection and the pinned head of every item) and the original 202 acknowledgment. The completion process holds every item from it. |
| Selected source meaning | The campaign admitted the owner's completion: the exact campaign revision, the frozen ClosureIntent (the attestation, the complete drained item selection and the pinned head of every item) and the original 202 acknowledgment. The completion process holds every item from it. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompletionAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewCampaignCompletionAccepted |
| Schema discriminator | bdgrz.access-review-campaign.access-review-campaign-completion-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewCampaign |
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

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T059](../reactors/access-review-completion-reactor.md#t059) | physical root, logical prerequisite | AccessReviewCampaign | AuthorizeAccessReviewItemCampaignCompletion | AccessReviewItem |
| [T060](../reactors/access-review-completion-reactor.md#t060) | physical root | AccessReviewCampaign | CloseAccessReviewItemCampaignCompletionAuthorization | AccessReviewItem |
| [T073](../reactors/access-review-completion-reactor.md#t073) | physical root, logical prerequisite | AccessReviewCampaign | CompleteAccessReviewCampaignClosure | AccessReviewCampaign |
| [A006](../reactors/unassigned-architecture-rows.md#a006) | physical root, logical prerequisite | AccessReviewCampaign | AbortAccessReviewCampaignCompletion | AccessReviewCampaign |

### AccessReviewCampaignCompletionCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewCampaignCompletionCanceled |
| Selected source event name | AccessReviewCampaignCompletionCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewCampaignCompletionCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewCampaignCompletionCanceled |
| Adopted wire | `{"discriminator":"bdgrz.access-review-campaign.access-review-campaign-completion-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompletionCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The pending completion of an access review campaign was canceled with a failure proof, so any delayed attempt to close that completion is refused and the campaign stays open. |
| Selected source meaning | The pending completion of an access review campaign was canceled with a failure proof, so any delayed attempt to close that completion is refused and the campaign stays open. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignCompletionCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewCampaignCompletionCanceled |
| Schema discriminator | bdgrz.access-review-campaign.access-review-campaign-completion-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewCampaign |
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

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T060](../reactors/access-review-completion-reactor.md#t060) | physical root, logical prerequisite | AccessReviewCampaign | CloseAccessReviewItemCampaignCompletionAuthorization | AccessReviewItem |
| [A006](../reactors/unassigned-architecture-rows.md#a006) | emitted semantic fact | AccessReviewCampaign | AbortAccessReviewCampaignCompletion | AccessReviewCampaign |

### AccessReviewCampaignLaunched

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewCampaignLaunched |
| Selected source event name | AccessReviewCampaignLaunched |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewCampaignLaunched |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewCampaignLaunched |
| Adopted wire | `{"discriminator":"bdgrz.access_review.campaign.launched","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignLaunched.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.campaign.launched","version":1}` |
| Target meaning | A campaign was launched with its frozen population, reviewers, instructions, and deadline. The reviewers and the complete item list are verified immutable content, so a campaign of any size is one bounded fact. ItemIntentManifest holds the frozen creation intent of every item in canonical item order; the launch fan-out materializes each item from it. The campaign's logical revision is 1 after launch. |
| Selected source meaning | A campaign was launched with its frozen population, reviewers, instructions, and deadline. The reviewers and the complete item list are verified immutable content, so a campaign of any size is one bounded fact. ItemIntentManifest holds the frozen creation intent of every item in canonical item order; the launch fan-out materializes each item from it. The campaign's logical revision is 1 after launch. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewCampaignLaunched.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewCampaignLaunched |
| Schema discriminator | bdgrz.access_review.campaign.launched |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewCampaign |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignLaunched.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<AccessReviewerView> to verified immutable content; preserve all items/order/identity, no truncation., Externalize complete IReadOnlyList<AccessReviewItemView> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Name | string | true |
| Instructions | string | true |
| Deadline | DateTimeOffset | true |
| SnapshotId | Uuid | true |
| ContentSha256 | string | true |
| ReviewersManifest | ImmutableContentRef | true |
| ItemsManifest | ImmutableContentRef | true |
| LaunchedBy | ActorReference | true |
| LaunchedAt | DateTimeOffset | true |
| ProgramId | Uuid? | false |
| RemediationOwnerMemberId | Uuid? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessReviewCampaignLaunched.ReviewersManifest.v1

| Property | Value |
| --- | --- |
| field | ReviewersManifest |
| schema | AccessReviewCampaignLaunched.ReviewersManifest.v1 |
| contentType | IReadOnlyList<AccessReviewerView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

#### Content schema: AccessReviewCampaignLaunched.ItemsManifest.v1

| Property | Value |
| --- | --- |
| field | ItemsManifest |
| schema | AccessReviewCampaignLaunched.ItemsManifest.v1 |
| contentType | IReadOnlyList<AccessReviewItemView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T008](../reactors/access-review-launch-fan-out-reactor.md#t008) | physical root, logical prerequisite | AccessReviewCampaign | OpenAccessReviewItem | AccessReviewItem |

### AccessReviewDecisionBatchAdmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewDecisionBatchAdmitted |
| Selected source event name | AccessReviewDecisionBatchAdmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewDecisionBatchAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewDecisionBatchAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.access-review-campaign.access-review-decision-batch-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The campaign admitted a complete bulk decision: the exact campaign revision, the frozen BulkAccessDecisionIntent (the preview token, the immutable selection with each item's frozen head and the shared decision and rationale) and the original 202 acknowledgment. The batch is created from this fact at its own revision zero, and a campaign completion drains it before it holds any item. |
| Selected source meaning | The campaign admitted a complete bulk decision: the exact campaign revision, the frozen BulkAccessDecisionIntent (the preview token, the immutable selection with each item's frozen head and the shared decision and rationale) and the original 202 acknowledgment. The batch is created from this fact at its own revision zero, and a campaign completion drains it before it holds any item. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewDecisionBatchAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewDecisionBatchAdmitted |
| Schema discriminator | bdgrz.access-review-campaign.access-review-decision-batch-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewCampaign |
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
| [T009](../reactors/access-review-bulk-admission-reactor.md#t009) | physical root | AccessReviewDecisionBatch | ReserveAccessReviewItemDecision | AccessReviewItem |
| [T010](../reactors/access-review-bulk-admission-reactor.md#t010) | physical root | AccessReviewDecisionBatch | ApplyAccessReviewItemDecision | AccessReviewItem |
| [T011](../reactors/access-review-bulk-admission-reactor.md#t011) | physical root | AccessReviewDecisionBatch | PublishAccessReviewDecisionBatch | AccessReviewDecisionBatch |
| [T080](../reactors/access-review-bulk-admission-reactor.md#t080) | physical root | AccessReviewDecisionBatch | CloseAccessReviewItemDecisionAuthorization | AccessReviewItem |
| [T086](../reactors/access-review-bulk-admission-reactor.md#t086) | physical root, logical prerequisite | AccessReviewCampaign | RecordAccessReviewDecisionBatch | AccessReviewDecisionBatch |
| [A005](../reactors/unassigned-architecture-rows.md#a005) | physical root, logical prerequisite | AccessReviewCampaign | CancelAccessReviewDecisionBatch | AccessReviewDecisionBatch |


## Wire/schema version discrepancies

- **AccessReviewCampaignCompleted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessReviewCampaignLaunched** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
