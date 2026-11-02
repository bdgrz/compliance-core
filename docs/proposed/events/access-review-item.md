# AccessReviewItem events

Canonical events: **11**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-review-item.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AccessDecisionsRecorded | business | bdgrz.access_review.decisions.recorded | 2 | 10 | The reviewer's decision on one item. DecisionsManifest is the complete original decision collection as verified content. A decision that belongs to a bulk batch names it in DecisionBatchId and stays hidden until the batch publishes (PublicationRequired); a single decision names no batch and is visible at once. |
| AccessRemediationChangeRecorded | business | bdgrz.access_review.remediation.change_recorded | 2 | 8 | A provider-side change was reported; the complete change is verified content. |
| AccessRemediationExceptionRecorded | business | bdgrz.access_review.remediation.exception_recorded | 2 | 8 | An exception to verified remediation was approved; the complete exception is verified content. |
| AccessRemediationVerified | business | bdgrz.access_review.remediation.verified | 2 | 8 | A later accepted population verified the remediation; the complete verification is verified content. |
| AccessReviewItemCampaignCompletionUseAuthorized | reservation | bdgrz.access-review-item.access-review-item-campaign-completion-use-authorized | 1 | 8 | The exact pinned head of one item is held for a campaign completion. The use does not advance the item's public revision; Revision is the held revision. |
| AccessReviewItemCampaignCompletionUseSettled | reservation | bdgrz.access-review-item.access-review-item-campaign-completion-use-settled | 1 | 8 | The campaign completion use of one item ended. Disposition is the one the verified terminal campaign fact requires: parent_closed keeps the item closed against every later change (the closure fence), canceled clears only this use after the parent cancellation fence. A settlement before any grant fences the key against late acquisition. |
| AccessReviewItemDecisionAuthorized | reservation | bdgrz.access-review-item.access-review-item-decision-authorized | 1 | 8 | The exact frozen head of one item is held for a bulk decision batch. The use does not advance the item's revision; Revision is the held revision. The item refuses every other change while the use is held. |
| AccessReviewItemDecisionCanceled | business | bdgrz.access-review-item.access-review-item-decision-canceled | 1 | 8 | A review item was fenced against a canceled bulk decision batch, so a delayed hold or application of that batch's decision can no longer change the item. |
| AccessReviewItemDecisionSettled | reservation | bdgrz.access-review-item.access-review-item-decision-settled | 1 | 8 | The bulk decision use of one item ended. Disposition is the one the verified terminal batch fact requires: irreversible_decision after the batch published, which makes the item's provisional decision of the batch visible, or canceled after the item fence, which keeps it hidden forever. A settlement before any hold fences the key against a late hold. |
| AccessReviewItemOpened | business | bdgrz.access-review-item.access-review-item-opened | 1 | 8 | One frozen item was materialized from the launch manifest of its campaign. The item's logical revision is 1. Intent is the frozen AccessReviewItemCreationIntent. |
| AccessReviewResponsibilityReassigned | business | bdgrz.access_review.responsibility.reassigned | 2 | 7 | A campaign responsibility was changed after its launch-frozen owner lost eligibility. |

## Event details

### AccessDecisionsRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessDecisionsRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessDecisionsRecorded |
| Adopted wire | `{"discriminator":"bdgrz.access_review.decisions.recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessDecisionsRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.decisions.recorded","version":1}` |
| Meaning | The reviewer's decision on one item. DecisionsManifest is the complete original decision collection as verified content. A decision that belongs to a bulk batch names it in DecisionBatchId and stays hidden until the batch publishes (PublicationRequired); a single decision names no batch and is visible at once. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessDecisionsRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessDecisionsRecorded |
| Schema discriminator | bdgrz.access_review.decisions.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessDecisionsRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize complete IReadOnlyList<AccessDecisionView> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Revision | long | true |
| DecisionsManifest | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| DecisionBatchId | Uuid? | true |
| PublicationRequired | bool | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessDecisionsRecorded.DecisionsManifest.v1

| Property | Value |
| --- | --- |
| field | DecisionsManifest |
| schema | AccessDecisionsRecorded.DecisionsManifest.v1 |
| contentType | IReadOnlyList<AccessDecisionView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

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
| [T010](../reactors/access-review-bulk-admission-reactor.md#t010) | emitted semantic fact | AccessReviewDecisionBatch | ApplyAccessReviewItemDecision | AccessReviewItem |

### AccessRemediationChangeRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessRemediationChangeRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessRemediationChangeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.access_review.remediation.change_recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationChangeRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.remediation.change_recorded","version":1}` |
| Meaning | A provider-side change was reported; the complete change is verified content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationChangeRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessRemediationChangeRecorded |
| Schema discriminator | bdgrz.access_review.remediation.change_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessRemediationChangeRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessRemediationChangeView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Revision | long | true |
| ItemId | Uuid | true |
| ChangeContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessRemediationChangeRecorded.ChangeContent.v1

| Property | Value |
| --- | --- |
| field | ChangeContent |
| schema | AccessRemediationChangeRecorded.ChangeContent.v1 |
| contentType | AccessRemediationChangeView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessRemediationExceptionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessRemediationExceptionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessRemediationExceptionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.access_review.remediation.exception_recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationExceptionRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.remediation.exception_recorded","version":1}` |
| Meaning | An exception to verified remediation was approved; the complete exception is verified content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationExceptionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessRemediationExceptionRecorded |
| Schema discriminator | bdgrz.access_review.remediation.exception_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessRemediationExceptionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessRemediationExceptionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Revision | long | true |
| ItemId | Uuid | true |
| ExceptionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessRemediationExceptionRecorded.ExceptionContent.v1

| Property | Value |
| --- | --- |
| field | ExceptionContent |
| schema | AccessRemediationExceptionRecorded.ExceptionContent.v1 |
| contentType | AccessRemediationExceptionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessRemediationVerified

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessRemediationVerified |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessRemediationVerified |
| Adopted wire | `{"discriminator":"bdgrz.access_review.remediation.verified","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationVerified.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.remediation.verified","version":1}` |
| Meaning | A later accepted population verified the remediation; the complete verification is verified content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessRemediationVerified.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessRemediationVerified |
| Schema discriminator | bdgrz.access_review.remediation.verified |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessRemediationVerified.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessRemediationVerificationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Revision | long | true |
| ItemId | Uuid | true |
| VerificationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessRemediationVerified.VerificationContent.v1

| Property | Value |
| --- | --- |
| field | VerificationContent |
| schema | AccessRemediationVerified.VerificationContent.v1 |
| contentType | AccessRemediationVerificationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessReviewItemCampaignCompletionUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewItemCampaignCompletionUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewItemCampaignCompletionUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.access-review-item.access-review-item-campaign-completion-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemCampaignCompletionUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The exact pinned head of one item is held for a campaign completion. The use does not advance the item's public revision; Revision is the held revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemCampaignCompletionUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewItemCampaignCompletionUseAuthorized |
| Schema discriminator | bdgrz.access-review-item.access-review-item-campaign-completion-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
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
| [T059](../reactors/access-review-completion-reactor.md#t059) | emitted semantic fact | AccessReviewCampaign | AuthorizeAccessReviewItemCampaignCompletionUse | AccessReviewItem |

### AccessReviewItemCampaignCompletionUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewItemCampaignCompletionUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewItemCampaignCompletionUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.access-review-item.access-review-item-campaign-completion-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemCampaignCompletionUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The campaign completion use of one item ended. Disposition is the one the verified terminal campaign fact requires: parent_closed keeps the item closed against every later change (the closure fence), canceled clears only this use after the parent cancellation fence. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemCampaignCompletionUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewItemCampaignCompletionUseSettled |
| Schema discriminator | bdgrz.access-review-item.access-review-item-campaign-completion-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
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
| [T060](../reactors/access-review-completion-reactor.md#t060) | emitted semantic fact | AccessReviewCampaign | SettleAccessReviewItemCampaignCompletionUse | AccessReviewItem |

### AccessReviewItemDecisionAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewItemDecisionAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewItemDecisionAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.access-review-item.access-review-item-decision-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemDecisionAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The exact frozen head of one item is held for a bulk decision batch. The use does not advance the item's revision; Revision is the held revision. The item refuses every other change while the use is held. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemDecisionAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewItemDecisionAuthorized |
| Schema discriminator | bdgrz.access-review-item.access-review-item-decision-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
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
| [T009](../reactors/access-review-bulk-admission-reactor.md#t009) | emitted semantic fact | AccessReviewDecisionBatch | ReserveAccessReviewItemDecision | AccessReviewItem |

### AccessReviewItemDecisionCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewItemDecisionCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewItemDecisionCanceled |
| Adopted wire | `{"discriminator":"bdgrz.access-review-item.access-review-item-decision-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemDecisionCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A review item was fenced against a canceled bulk decision batch, so a delayed hold or application of that batch's decision can no longer change the item. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemDecisionCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewItemDecisionCanceled |
| Schema discriminator | bdgrz.access-review-item.access-review-item-decision-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
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
| [T012](../reactors/access-review-bulk-cancellation-reactor.md#t012) | emitted semantic fact | AccessReviewDecisionBatch | FenceAccessReviewItemDecision | AccessReviewItem |

### AccessReviewItemDecisionSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewItemDecisionSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewItemDecisionSettled |
| Adopted wire | `{"discriminator":"bdgrz.access-review-item.access-review-item-decision-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemDecisionSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The bulk decision use of one item ended. Disposition is the one the verified terminal batch fact requires: irreversible_decision after the batch published, which makes the item's provisional decision of the batch visible, or canceled after the item fence, which keeps it hidden forever. A settlement before any hold fences the key against a late hold. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemDecisionSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewItemDecisionSettled |
| Schema discriminator | bdgrz.access-review-item.access-review-item-decision-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
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
| [T080](../reactors/access-review-bulk-admission-reactor.md#t080) | emitted semantic fact | AccessReviewDecisionBatch | SettleAccessReviewItemDecision | AccessReviewItem |

### AccessReviewItemOpened

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewItemOpened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewItemOpened |
| Adopted wire | `{"discriminator":"bdgrz.access-review-item.access-review-item-opened","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemOpened.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | One frozen item was materialized from the launch manifest of its campaign. The item's logical revision is 1. Intent is the frozen AccessReviewItemCreationIntent. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewItemOpened.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewItemOpened |
| Schema discriminator | bdgrz.access-review-item.access-review-item-opened |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
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

#### Content schema: access-review-item-creation-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | access-review-item-creation-intent.v1 |
| recordType | AccessReviewItemCreationIntent |
| rule | Exact item intent comes from the complete launch manifest. Stable item identity and original reviewer/personal eligibility facts are frozen. Late materialization after parent seal must complete before closure; no item can be omitted from coverage. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| ItemId | Uuid | true |
| SystemInstanceId | Uuid | true |
| PopulationEntry | ImmutableContentRef | true |
| ReviewerMemberId | Uuid | true |
| ReviewerEpisodeId | Uuid | true |
| SnapshotProof | SourceFactLocator | true |

##### Variant: AccessReviewItemCreationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| CampaignId | Uuid | true |
| CampaignGeneration | ulong | true |
| ItemId | Uuid | true |
| SystemInstanceId | Uuid | true |
| PopulationEntry | ImmutableContentRef | true |
| ReviewerMemberId | Uuid | true |
| ReviewerEpisodeId | Uuid | true |
| SnapshotProof | SourceFactLocator | true |


#### Resolved intent schema

AccessReviewItemCreationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T008](../reactors/access-review-launch-fan-out-reactor.md#t008) | emitted semantic fact | AccessReviewCampaign | MaterializeAccessReviewItem | AccessReviewItem |

### AccessReviewResponsibilityReassigned

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewResponsibilityReassigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewResponsibilityReassigned |
| Adopted wire | `{"discriminator":"bdgrz.access_review.responsibility.reassigned","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewResponsibilityReassigned.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_review.responsibility.reassigned","version":1}` |
| Meaning | A campaign responsibility was changed after its launch-frozen owner lost eligibility. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessReviewResponsibilityReassigned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessReviewResponsibilityReassigned |
| Schema discriminator | bdgrz.access_review.responsibility.reassigned |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessReviewItem |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessReviewResponsibilityReassigned.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessReviewResponsibilityReassignmentView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| CampaignId | Uuid | true |
| Revision | long | true |
| ReassignmentContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessReviewResponsibilityReassigned.ReassignmentContent.v1

| Property | Value |
| --- | --- |
| field | ReassignmentContent |
| schema | AccessReviewResponsibilityReassigned.ReassignmentContent.v1 |
| contentType | AccessReviewResponsibilityReassignmentView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AccessDecisionsRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessRemediationChangeRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessRemediationExceptionRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessRemediationVerified** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessReviewResponsibilityReassigned** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
