# RiskTreatmentAction events

Canonical events: **7**. Complete canonical event and available schema records: [owner event JSON](../data/events/risk-treatment-action.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| RiskTreatmentActionAdded | RiskTreatmentActionAdded | business | bdgrz.risk.treatment_action.added | 2 | 8 | Accountable treatment work was added to the risk. ActionContent is the complete open RiskTreatmentActionView; the logical id of the operation is the action id and the revision is one. |
| RiskTreatmentActionCancelled | RiskTreatmentActionCancelled | business | bdgrz.risk.treatment_action.cancelled | 2 | 12 | Unfinished treatment work was canceled while its history is retained. |
| RiskTreatmentActionCompletionReviewAccepted | RiskTreatmentActionCompletionReviewAccepted | business | bdgrz.risk-treatment-action.risk-treatment-action-completion-review-accepted | 1 | 8 | The root native acceptance of a completion review. It seals the exact submission for Generation, which is the submission ordinal of the action, and references the frozen RiskActionCompletionReviewIntent; the action only completes or reopens once RiskTreatmentActionCompletionReviewed follows. |
| RiskTreatmentActionCompletionReviewCanceled | RiskTreatmentActionCompletionReviewCanceled | business | bdgrz.risk-treatment-action.risk-treatment-action-completion-review-canceled | 1 | 8 | A pending completion review ended before it recorded its decision. The fact is the local receiver fence: a delayed CompleteRiskTreatmentActionReview sees it and does nothing, and only then are the evidence uses settled. SourceProofs is the failure proof. |
| RiskTreatmentActionCompletionReviewed | RiskTreatmentActionCompletionReviewed | business | bdgrz.risk.treatment_action.completion_reviewed | 2 | 15 | The decision of a completion review, appended by RiskActionCompletionReviewProcess once every cited evidence request is held. Intent is the frozen RiskActionCompletionReviewIntent. |
| RiskTreatmentActionCompletionSubmitted | RiskTreatmentActionCompletionSubmitted | business | bdgrz.risk.treatment_action.completion_submitted | 2 | 14 | Treatment work was reported done. EvidenceRequestIdsManifest is the complete ordered list of the fulfilled evidence requests the submission rests on; the logical id of the operation is the submission id. |
| RiskTreatmentActionRevised | RiskTreatmentActionRevised | business | bdgrz.risk.treatment_action.revised | 2 | 18 | Open treatment work was revised. EvidenceRequestIdsManifest is the complete ordered list of cited evidence request ids. |

## Event details

### RiskTreatmentActionAdded

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionAdded |
| Selected source event name | RiskTreatmentActionAdded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionAdded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionAdded |
| Adopted wire | `{"discriminator":"bdgrz.risk.treatment_action.added","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionAdded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.treatment_action.added","version":1}` |
| Target meaning | Accountable treatment work was added to the risk. ActionContent is the complete open RiskTreatmentActionView; the logical id of the operation is the action id and the revision is one. |
| Selected source meaning | Accountable treatment work was added to the risk. ActionContent is the complete open RiskTreatmentActionView; the logical id of the operation is the action id and the revision is one. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionAdded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionAdded |
| Schema discriminator | bdgrz.risk.treatment_action.added |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskTreatmentActionAdded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured RiskTreatmentActionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| ActionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: RiskTreatmentActionAdded.ActionContent.v1

| Property | Value |
| --- | --- |
| field | ActionContent |
| schema | RiskTreatmentActionAdded.ActionContent.v1 |
| contentType | RiskTreatmentActionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskTreatmentActionCancelled

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionCancelled |
| Selected source event name | RiskTreatmentActionCancelled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionCancelled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionCancelled |
| Adopted wire | `{"discriminator":"bdgrz.risk.treatment_action.cancelled","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCancelled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.treatment_action.cancelled","version":1}` |
| Target meaning | Unfinished treatment work was canceled while its history is retained. |
| Selected source meaning | Unfinished treatment work was canceled while its history is retained. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCancelled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionCancelled |
| Schema discriminator | bdgrz.risk.treatment_action.cancelled |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskTreatmentActionCancelled.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| ActionId | Uuid | true |
| CancellationId | Uuid | true |
| Rationale | string | true |
| CancelledBy | ActorReference | true |
| CancelledAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskTreatmentActionCompletionReviewAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionCompletionReviewAccepted |
| Selected source event name | RiskTreatmentActionCompletionReviewAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionCompletionReviewAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionCompletionReviewAccepted |
| Adopted wire | `{"discriminator":"bdgrz.risk-treatment-action.risk-treatment-action-completion-review-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The root native acceptance of a completion review. It seals the exact submission for Generation, which is the submission ordinal of the action, and references the frozen RiskActionCompletionReviewIntent; the action only completes or reopens once RiskTreatmentActionCompletionReviewed follows. |
| Selected source meaning | The root native acceptance of a completion review. It seals the exact submission for Generation, which is the submission ordinal of the action, and references the frozen RiskActionCompletionReviewIntent; the action only completes or reopens once RiskTreatmentActionCompletionReviewed follows. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionCompletionReviewAccepted |
| Schema discriminator | bdgrz.risk-treatment-action.risk-treatment-action-completion-review-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
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

#### Content schema: risk-action-completion-review-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | risk-action-completion-review-intent.v1 |
| recordType | RiskActionCompletionReviewIntent |
| rule | Freeze exact submitted completion and independent reviewer/waiver/decision. All current fulfilled evidence uses remain held through accepted native review or local cancellation. New submission needs a new operation identity. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| CompletionEvidence | ImmutableSelectionManifest | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: RiskActionCompletionReviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| CompletionEvidence | ImmutableSelectionManifest | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

RiskActionCompletionReviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T071](../reactors/risk-action-completion-review-reactor.md#t071) | physical root, logical prerequisite | RiskTreatmentAction | AuthorizeEvidenceRequestActionCompletion | EvidenceRequest |
| [T072](../reactors/risk-action-completion-review-reactor.md#t072) | physical root | RiskTreatmentAction | CloseEvidenceRequestActionCompletionAuthorization | EvidenceRequest |
| [T078](../reactors/risk-action-completion-review-reactor.md#t078) | physical root, logical prerequisite | RiskTreatmentAction | CompleteRiskTreatmentActionReview | RiskTreatmentAction |
| [A012](../reactors/unassigned-domain-choreography.md#a012) | physical root, logical prerequisite | RiskTreatmentAction | AbortRiskTreatmentActionReview | RiskTreatmentAction |

### RiskTreatmentActionCompletionReviewCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionCompletionReviewCanceled |
| Selected source event name | RiskTreatmentActionCompletionReviewCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionCompletionReviewCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionCompletionReviewCanceled |
| Adopted wire | `{"discriminator":"bdgrz.risk-treatment-action.risk-treatment-action-completion-review-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A pending completion review ended before it recorded its decision. The fact is the local receiver fence: a delayed CompleteRiskTreatmentActionReview sees it and does nothing, and only then are the evidence uses settled. SourceProofs is the failure proof. |
| Selected source meaning | A pending completion review ended before it recorded its decision. The fact is the local receiver fence: a delayed CompleteRiskTreatmentActionReview sees it and does nothing, and only then are the evidence uses settled. SourceProofs is the failure proof. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionCompletionReviewCanceled |
| Schema discriminator | bdgrz.risk-treatment-action.risk-treatment-action-completion-review-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
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

#### Content schema: risk-action-completion-review-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | risk-action-completion-review-intent.v1 |
| recordType | RiskActionCompletionReviewIntent |
| rule | Freeze exact submitted completion and independent reviewer/waiver/decision. All current fulfilled evidence uses remain held through accepted native review or local cancellation. New submission needs a new operation identity. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| CompletionEvidence | ImmutableSelectionManifest | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: RiskActionCompletionReviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| CompletionEvidence | ImmutableSelectionManifest | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

RiskActionCompletionReviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T072](../reactors/risk-action-completion-review-reactor.md#t072) | logical prerequisite | RiskTreatmentAction | CloseEvidenceRequestActionCompletionAuthorization | EvidenceRequest |
| [A012](../reactors/unassigned-domain-choreography.md#a012) | emitted semantic fact | RiskTreatmentAction | AbortRiskTreatmentActionReview | RiskTreatmentAction |

### RiskTreatmentActionCompletionReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionCompletionReviewed |
| Selected source event name | RiskTreatmentActionCompletionReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionCompletionReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionCompletionReviewed |
| Adopted wire | `{"discriminator":"bdgrz.risk.treatment_action.completion_reviewed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.treatment_action.completion_reviewed","version":1}` |
| Target meaning | The decision of a completion review, appended by RiskActionCompletionReviewProcess once every cited evidence request is held. Intent is the frozen RiskActionCompletionReviewIntent. |
| Selected source meaning | The decision of a completion review, appended by RiskActionCompletionReviewProcess once every cited evidence request is held. Intent is the frozen RiskActionCompletionReviewIntent. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionCompletionReviewed |
| Schema discriminator | bdgrz.risk.treatment_action.completion_reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskTreatmentActionCompletionReviewed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| ActionId | Uuid | true |
| DecisionId | Uuid | true |
| Outcome | string | true |
| Rationale | string | true |
| Reviewer | ActorReference | true |
| ReviewedAt | DateTimeOffset | true |
| SeparationOfDutiesWaiverId | Uuid? | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: risk-action-completion-review-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | risk-action-completion-review-intent.v1 |
| recordType | RiskActionCompletionReviewIntent |
| rule | Freeze exact submitted completion and independent reviewer/waiver/decision. All current fulfilled evidence uses remain held through accepted native review or local cancellation. New submission needs a new operation identity. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| CompletionEvidence | ImmutableSelectionManifest | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |

##### Variant: RiskActionCompletionReviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| AuthoredReview | ImmutableContentRef | true |
| CompletionEvidence | ImmutableSelectionManifest | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |


#### Resolved intent schema

RiskActionCompletionReviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T072](../reactors/risk-action-completion-review-reactor.md#t072) | logical prerequisite | RiskTreatmentAction | CloseEvidenceRequestActionCompletionAuthorization | EvidenceRequest |
| [T078](../reactors/risk-action-completion-review-reactor.md#t078) | emitted semantic fact | RiskTreatmentAction | CompleteRiskTreatmentActionReview | RiskTreatmentAction |

### RiskTreatmentActionCompletionSubmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionCompletionSubmitted |
| Selected source event name | RiskTreatmentActionCompletionSubmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionCompletionSubmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionCompletionSubmitted |
| Adopted wire | `{"discriminator":"bdgrz.risk.treatment_action.completion_submitted","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionSubmitted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.treatment_action.completion_submitted","version":1}` |
| Target meaning | Treatment work was reported done. EvidenceRequestIdsManifest is the complete ordered list of the fulfilled evidence requests the submission rests on; the logical id of the operation is the submission id. |
| Selected source meaning | Treatment work was reported done. EvidenceRequestIdsManifest is the complete ordered list of the fulfilled evidence requests the submission rests on; the logical id of the operation is the submission id. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionCompletionSubmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionCompletionSubmitted |
| Schema discriminator | bdgrz.risk.treatment_action.completion_submitted |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskTreatmentActionCompletionSubmitted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<Uuid> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| ActionId | Uuid | true |
| SubmissionId | Uuid | true |
| Summary | string | true |
| EvidenceRequestIdsManifest | ImmutableContentRef | true |
| SubmitterMemberId | Uuid | true |
| SubmittedBy | ActorReference | true |
| SubmittedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: RiskTreatmentActionCompletionSubmitted.EvidenceRequestIdsManifest.v1

| Property | Value |
| --- | --- |
| field | EvidenceRequestIdsManifest |
| schema | RiskTreatmentActionCompletionSubmitted.EvidenceRequestIdsManifest.v1 |
| contentType | IReadOnlyList<Uuid> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskTreatmentActionRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskTreatmentActionRevised |
| Selected source event name | RiskTreatmentActionRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskTreatmentActionRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskTreatmentActionRevised |
| Adopted wire | `{"discriminator":"bdgrz.risk.treatment_action.revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionRevised.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.treatment_action.revised","version":1}` |
| Target meaning | Open treatment work was revised. EvidenceRequestIdsManifest is the complete ordered list of cited evidence request ids. |
| Selected source meaning | Open treatment work was revised. EvidenceRequestIdsManifest is the complete ordered list of cited evidence request ids. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskTreatmentActionRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskTreatmentActionRevised |
| Schema discriminator | bdgrz.risk.treatment_action.revised |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAction |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskTreatmentActionRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<Uuid> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| ActionId | Uuid | true |
| RequestId | Uuid | true |
| ExpectedRevision | long | true |
| Title | string | true |
| TargetState | string | true |
| ExpectedEvidence | string | true |
| DueOn | DateOnly | true |
| AccountableMemberId | Uuid | true |
| EvidenceRequestIdsManifest | ImmutableContentRef | true |
| RevisedBy | ActorReference | true |
| RevisedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: RiskTreatmentActionRevised.EvidenceRequestIdsManifest.v1

| Property | Value |
| --- | --- |
| field | EvidenceRequestIdsManifest |
| schema | RiskTreatmentActionRevised.EvidenceRequestIdsManifest.v1 |
| contentType | IReadOnlyList<Uuid> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **RiskTreatmentActionAdded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskTreatmentActionCancelled** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskTreatmentActionCompletionReviewed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskTreatmentActionCompletionSubmitted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskTreatmentActionRevised** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
