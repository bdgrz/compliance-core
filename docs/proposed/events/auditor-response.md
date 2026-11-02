# AuditorResponse events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/auditor-response.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditResponseDraftCreated | business | bdgrz.auditorrequests.audit-response-draft-created | 1 | 9 | The materialization of an admitted response draft on its own owner, at revision one. Change repeats the frozen ChildAdmissionIntent the request accepted; the response never loads the live request. The actor is the original author of the draft. |
| AuditResponseDraftRevised | business | bdgrz.auditorrequests.audit-response-draft-revised | 1 | 9 | A draft or rejected response was revised into the next draft version. Change is the complete AuditorResponseVersionView of that version. |
| AuditorResponseDelivered | business | bdgrz.auditorrequests.auditor-response-delivered | 1 | 9 | The exact version of a response was intentionally delivered. Change is the AuditorResponseDeliveryView; the delivery is immutable. |
| AuditorResponseDisclosureReviewed | business | bdgrz.auditorrequests.auditor-response-disclosure-reviewed | 1 | 9 | The independent disclosure review of the exact submitted version. Change is the AuditorResponseReviewView; Outcome is approved or rejected. |
| AuditorResponseDispositionRecorded | business | bdgrz.auditorrequests.auditor-response-disposition-recorded | 1 | 9 | The external disposition of the exact delivered version. Change is the AuditorResponseDispositionView; the disposition is immutable. |
| AuditorResponseRequestResolutionUseAuthorized | reservation | bdgrz.auditor-response.auditor-response-request-resolution-use-authorized | 1 | 8 | A response with an accepted external disposition is held for a request resolution. The use does not advance the public response revision: Revision is the held revision and Generation the request generation the resolution seals. |
| AuditorResponseRequestResolutionUseSettled | reservation | bdgrz.auditor-response.auditor-response-request-resolution-use-settled | 1 | 8 | A request-resolution use of a response ended. Disposition repeats the disposition of the settlement intent so the response can install the closed-parent fence from its own ordered stream: a SourceUseSettlementDisposition.ParentClosed settlement keeps the response under the resolved request generation, and releasing the temporary use never restores an unrestricted writable response. |
| AuditorResponseSubmitted | business | bdgrz.auditorrequests.auditor-response-submitted | 1 | 9 | The exact draft version of a response was submitted. Change is the AuditorResponseSubmissionView and Version the business version it submitted. |

## Event details

### AuditResponseDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditResponseDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditResponseDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.audit-response-draft-created","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditResponseDraftCreated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The materialization of an admitted response draft on its own owner, at revision one. Change repeats the frozen ChildAdmissionIntent the request accepted; the response never loads the live request. The actor is the original author of the draft. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditResponseDraftCreated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditResponseDraftCreated |
| Schema discriminator | bdgrz.auditorrequests.audit-response-draft-created |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditResponseDraftCreated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: child-admission-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | child-admission-intent.v1 |
| recordType | ChildAdmissionIntent |
| rule | Concrete child payload schemas are the corresponding normalized Common requests: advisor feedback, auditor response/comment, corrective action, policy acknowledgement/training/waiver. Preserve all authored fields and original actor; child reducer never loads live parent. No later close/revoke discards admitted work. |
| sourceCommandVariants | MaterializeAuditorResponse |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| ChildKind | AdmittedChildKind | true |
| ChildId | Uuid | true |
| CanonicalAuthoredContent | ImmutableContentRef | true |
| SelectedSourceProofs | ImmutableSelectionManifest | true |
| ParentAdmissionRevision | ulong | true |

##### Variant: ChildAdmissionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| ChildKind | AdmittedChildKind | true |
| ChildId | Uuid | true |
| CanonicalAuthoredContent | ImmutableContentRef | true |
| SelectedSourceProofs | ImmutableSelectionManifest | true |
| ParentAdmissionRevision | ulong | true |


#### Resolved intent schema

ChildAdmissionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T033](../reactors/auditor-response-materialization-reactor.md#t033) | emitted semantic fact | AuditorRequest | MaterializeAuditorResponse | AuditorResponse |

### AuditResponseDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditResponseDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditResponseDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.audit-response-draft-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditResponseDraftRevised.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A draft or rejected response was revised into the next draft version. Change is the complete AuditorResponseVersionView of that version. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditResponseDraftRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditResponseDraftRevised |
| Schema discriminator | bdgrz.auditorrequests.audit-response-draft-revised |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditResponseDraftRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AuditResponseDraftRevised.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditResponseDraftRevised.change.v1 |
| sourceCommandVariants | ReviseAuditResponseDraft |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorResponseDelivered

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorResponseDelivered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseDelivered |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-response-delivered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDelivered.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The exact version of a response was intentionally delivered. Change is the AuditorResponseDeliveryView; the delivery is immutable. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDelivered.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseDelivered |
| Schema discriminator | bdgrz.auditorrequests.auditor-response-delivered |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorResponseDelivered.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AuditorResponseDelivered.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorResponseDelivered.change.v1 |
| sourceCommandVariants | DeliverAuditResponse |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorResponseDisclosureReviewed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorResponseDisclosureReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseDisclosureReviewed |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-response-disclosure-reviewed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDisclosureReviewed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The independent disclosure review of the exact submitted version. Change is the AuditorResponseReviewView; Outcome is approved or rejected. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDisclosureReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseDisclosureReviewed |
| Schema discriminator | bdgrz.auditorrequests.auditor-response-disclosure-reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorResponseDisclosureReviewed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AuditorResponseDisclosureReviewed.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorResponseDisclosureReviewed.change.v1 |
| sourceCommandVariants | ReviewAuditResponse |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorResponseDispositionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorResponseDispositionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseDispositionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-response-disposition-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDispositionRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The external disposition of the exact delivered version. Change is the AuditorResponseDispositionView; the disposition is immutable. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseDispositionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseDispositionRecorded |
| Schema discriminator | bdgrz.auditorrequests.auditor-response-disposition-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorResponseDispositionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AuditorResponseDispositionRecorded.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorResponseDispositionRecorded.change.v1 |
| sourceCommandVariants | RecordAuditorResponseDisposition |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorResponseRequestResolutionUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorResponseRequestResolutionUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseRequestResolutionUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.auditor-response.auditor-response-request-resolution-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseRequestResolutionUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A response with an accepted external disposition is held for a request resolution. The use does not advance the public response revision: Revision is the held revision and Generation the request generation the resolution seals. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseRequestResolutionUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseRequestResolutionUseAuthorized |
| Schema discriminator | bdgrz.auditor-response.auditor-response-request-resolution-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
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
| [T061](../reactors/auditor-request-resolution-reactor.md#t061) | emitted semantic fact | AuditorRequest | AuthorizeAuditorResponseRequestResolutionUse | AuditorResponse |

### AuditorResponseRequestResolutionUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorResponseRequestResolutionUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseRequestResolutionUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.auditor-response.auditor-response-request-resolution-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseRequestResolutionUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A request-resolution use of a response ended. Disposition repeats the disposition of the settlement intent so the response can install the closed-parent fence from its own ordered stream: a SourceUseSettlementDisposition.ParentClosed settlement keeps the response under the resolved request generation, and releasing the temporary use never restores an unrestricted writable response. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseRequestResolutionUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseRequestResolutionUseSettled |
| Schema discriminator | bdgrz.auditor-response.auditor-response-request-resolution-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
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
| [T062](../reactors/auditor-request-resolution-reactor.md#t062) | emitted semantic fact | AuditorRequest | SettleAuditorResponseRequestResolutionUse | AuditorResponse |

### AuditorResponseSubmitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorResponseSubmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseSubmitted |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-response-submitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseSubmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The exact draft version of a response was submitted. Change is the AuditorResponseSubmissionView and Version the business version it submitted. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseSubmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseSubmitted |
| Schema discriminator | bdgrz.auditorrequests.auditor-response-submitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorResponse |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorResponseSubmitted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AuditorResponseSubmitted.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorResponseSubmitted.change.v1 |
| sourceCommandVariants | SubmitAuditResponse |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
