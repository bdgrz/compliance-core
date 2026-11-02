# EvidenceRequest events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/evidence-request.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| EvidenceRequestActionCompletionUseAuthorized | reservation | bdgrz.evidence-request.evidence-request-action-completion-use-authorized | 1 | 8 | A fulfilled evidence request is held for a risk action completion review. The use does not advance the public request revision: Revision is the held revision, and Generation is the action's submission generation the review seals. |
| EvidenceRequestActionCompletionUseSettled | reservation | bdgrz.evidence-request.evidence-request-action-completion-use-settled | 1 | 8 | A risk action completion review use of an evidence request ended. The settlement intent records whether the review reached its irreversible decision or was canceled; a settlement before any grant fences the key. |
| EvidenceRequestCancelled | business | bdgrz.evidence.request.cancelled | 2 | 10 | Cancels an open request with an attributed rationale; cancellation is final. |
| EvidenceRequestFulfilled | business | bdgrz.evidence.request.fulfilled | 2 | 10 | Fulfils a request by linking the evidence artifact that answers it; fulfilment is final. |
| EvidenceRequestOpened | business | bdgrz.evidence.request.opened | 2 | 13 | Opens a request for one piece of evidence, owned by a member and due on a date. It is the native acceptance of OpenEvidenceRequest and the request's revision one. |

## Event details

### EvidenceRequestActionCompletionUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRequestActionCompletionUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRequestActionCompletionUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.evidence-request.evidence-request-action-completion-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestActionCompletionUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A fulfilled evidence request is held for a risk action completion review. The use does not advance the public request revision: Revision is the held revision, and Generation is the action's submission generation the review seals. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestActionCompletionUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceRequestActionCompletionUseAuthorized |
| Schema discriminator | bdgrz.evidence-request.evidence-request-action-completion-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceRequest |
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
| [T071](../reactors/risk-action-completion-review-reactor.md#t071) | emitted semantic fact | RiskTreatmentAction | AuthorizeEvidenceRequestActionCompletionUse | EvidenceRequest |

### EvidenceRequestActionCompletionUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRequestActionCompletionUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRequestActionCompletionUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.evidence-request.evidence-request-action-completion-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestActionCompletionUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A risk action completion review use of an evidence request ended. The settlement intent records whether the review reached its irreversible decision or was canceled; a settlement before any grant fences the key. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestActionCompletionUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceRequestActionCompletionUseSettled |
| Schema discriminator | bdgrz.evidence-request.evidence-request-action-completion-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceRequest |
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
| [T072](../reactors/risk-action-completion-review-reactor.md#t072) | emitted semantic fact | RiskTreatmentAction | SettleEvidenceRequestActionCompletionUse | EvidenceRequest |

### EvidenceRequestCancelled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRequestCancelled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRequestCancelled |
| Adopted wire | `{"discriminator":"bdgrz.evidence.request.cancelled","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestCancelled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.evidence.request.cancelled","version":1}` |
| Meaning | Cancels an open request with an attributed rationale; cancellation is final. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestCancelled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceRequestCancelled |
| Schema discriminator | bdgrz.evidence.request.cancelled |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceRequest |
| Schema existingContract | src/Compliance.Common/Features/Evidence/EvidenceRequestCancelled.cs |
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
| EvidenceRequestId | Uuid | true |
| Revision | long | true |
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

### EvidenceRequestFulfilled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRequestFulfilled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRequestFulfilled |
| Adopted wire | `{"discriminator":"bdgrz.evidence.request.fulfilled","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestFulfilled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.evidence.request.fulfilled","version":1}` |
| Meaning | Fulfils a request by linking the evidence artifact that answers it; fulfilment is final. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestFulfilled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceRequestFulfilled |
| Schema discriminator | bdgrz.evidence.request.fulfilled |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceRequest |
| Schema existingContract | src/Compliance.Common/Features/Evidence/EvidenceRequestFulfilled.cs |
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
| EvidenceRequestId | Uuid | true |
| Revision | long | true |
| ArtifactId | Uuid | true |
| FulfilledBy | ActorReference | true |
| FulfilledAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### EvidenceRequestOpened

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRequestOpened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRequestOpened |
| Adopted wire | `{"discriminator":"bdgrz.evidence.request.opened","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestOpened.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.evidence.request.opened","version":1}` |
| Meaning | Opens a request for one piece of evidence, owned by a member and due on a date. It is the native acceptance of OpenEvidenceRequest and the request's revision one. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRequestOpened.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceRequestOpened |
| Schema discriminator | bdgrz.evidence.request.opened |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceRequest |
| Schema existingContract | src/Compliance.Common/Features/Evidence/EvidenceRequestOpened.cs |
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
| EvidenceRequestId | Uuid | true |
| Title | string | true |
| Instructions | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| ControlId | Uuid? | true |
| RequestedBy | ActorReference | true |
| OpenedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **EvidenceRequestCancelled** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **EvidenceRequestFulfilled** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **EvidenceRequestOpened** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
