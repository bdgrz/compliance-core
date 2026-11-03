# AuditorRequest events

Canonical events: **10**. Complete canonical event and available schema records: [owner event JSON](../data/events/auditor-request.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AuditorCommentAdmitted | AuditorCommentAdmitted | business | bdgrz.auditor-request.auditor-comment-admitted | 1 | 8 | The open request accepted one comment. Intent is the frozen ChildAdmissionIntent with the complete body, compartment and original actor. The logical id of Operation is the comment id, which is also the identity of the thread that materializes it. |
| AuditorRequestAssigned | AuditorRequestAssigned | business | bdgrz.auditorrequests.auditor-request-assigned | 1 | 9 | An open request was assigned. Change is the complete AuditorRequestTermsContent after the assignment. |
| AuditorRequestRecorded | AuditorRequestRecorded | business | bdgrz.auditorrequests.auditor-request-recorded | 1 | 9 | The auditor request was recorded as the root native fact of its operation. Change is the verified immutable AuditorRequestTermsContent of the first terms; the request starts at generation one. |
| AuditorRequestReopened | AuditorRequestReopened | business | bdgrz.auditorrequests.auditor-request-reopened | 1 | 9 | A resolved request was reopened as the root native fact of its operation. The request generation advances and Change is the AuditorRequestReopenContent. |
| AuditorRequestResolutionAccepted | AuditorRequestResolutionAccepted | business | bdgrz.auditor-request.auditor-request-resolution-accepted | 1 | 8 | The root native acceptance of a resolution. It seals response, comment and sample admission for Generation and references the frozen ClosureIntent; the request only resolves once AuditorRequestResolved follows. |
| AuditorRequestResolutionCanceled | AuditorRequestResolutionCanceled | business | bdgrz.auditor-request.auditor-request-resolution-canceled | 1 | 8 | A pending resolution ended before it resolved the request. The fact is the local receiver fence: a delayed CompleteAuditorRequestResolution sees it and does nothing, and only then are the response uses settled. SourceProofs is the failure proof. Admission reopens for the generation. |
| AuditorRequestResolved | AuditorRequestResolved | business | bdgrz.auditorrequests.auditor-request-resolved | 1 | 11 | The request was resolved by the exact pending resolution. Change is the frozen ClosureIntent of the resolution, ResponseDispositionProofs the complete manifest of the held accepted dispositions of every admitted response and RequestGeneration the generation the resolution sealed. |
| AuditorRequestRevised | AuditorRequestRevised | business | bdgrz.auditorrequests.auditor-request-revised | 1 | 9 | The wording, scope, priority or due date of an open request was revised. Change is the complete AuditorRequestTermsContent after the revision. |
| AuditorResponseAdmitted | AuditorResponseAdmitted | business | bdgrz.auditor-request.auditor-response-admitted | 1 | 8 | The open request accepted one response draft for its current generation. Intent is the frozen ChildAdmissionIntent that carries the complete draft; the response owner is materialized independently, and a later resolution drains it instead of discarding it. The logical id of Operation is the response id. |
| AuditorSampleSelectionAdmitted | AuditorSampleSelectionAdmitted | business | bdgrz.auditor-request.auditor-sample-selection-admitted | 1 | 8 | The open request accepted one exact sample selection. Intent is the frozen AuditorSampleSelectionIntent with the complete population version content and the nonempty duplicate-free row selection. The population, its version and the superseded selection are repeated as facts so the request can enforce amendment lineage from its own ordered stream. The logical id of Operation is the selection id. |

## Event details

### AuditorCommentAdmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorCommentAdmitted |
| Selected source event name | AuditorCommentAdmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorCommentAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorCommentAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.auditor-request.auditor-comment-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorCommentAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The open request accepted one comment. Intent is the frozen ChildAdmissionIntent with the complete body, compartment and original actor. The logical id of Operation is the comment id, which is also the identity of the thread that materializes it. |
| Selected source meaning | The open request accepted one comment. Intent is the frozen ChildAdmissionIntent with the complete body, compartment and original actor. The logical id of Operation is the comment id, which is also the identity of the thread that materializes it. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorCommentAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorCommentAdmitted |
| Schema discriminator | bdgrz.auditor-request.auditor-comment-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
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

#### Content schema: child-admission-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | child-admission-intent.v1 |
| recordType | ChildAdmissionIntent |
| rule | Concrete child payload schemas are the corresponding normalized Common requests: advisor feedback, auditor response/comment, corrective action, policy acknowledgement/training/waiver. Preserve all authored fields and original actor; child reducer never loads live parent. No later close/revoke discards admitted work. |

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
| [T034](../reactors/auditor-comment-materialization-reactor.md#t034) | physical root, logical prerequisite | AuditorRequest | RecordAuditorComment | AuditorFeedbackThread |

### AuditorRequestAssigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestAssigned |
| Selected source event name | AuditorRequestAssigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestAssigned |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-request-assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestAssigned.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An open request was assigned. Change is the complete AuditorRequestTermsContent after the assignment. |
| Selected source meaning | An open request was assigned. Change is the complete AuditorRequestTermsContent after the assignment. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestAssigned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestAssigned |
| Schema discriminator | bdgrz.auditorrequests.auditor-request-assigned |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorRequestAssigned.cs |
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

#### Content schema: AuditorRequestAssigned.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorRequestAssigned.change.v1 |
| sourceCommandVariants | AssignAuditorRequest |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorRequestRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestRecorded |
| Selected source event name | AuditorRequestRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestRecorded |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-request-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The auditor request was recorded as the root native fact of its operation. Change is the verified immutable AuditorRequestTermsContent of the first terms; the request starts at generation one. |
| Selected source meaning | The auditor request was recorded as the root native fact of its operation. Change is the verified immutable AuditorRequestTermsContent of the first terms; the request starts at generation one. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestRecorded |
| Schema discriminator | bdgrz.auditorrequests.auditor-request-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorRequestRecorded.cs |
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

#### Content schema: AuditorRequestRecorded.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorRequestRecorded.change.v1 |
| sourceCommandVariants | RecordAuditorRequest |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorRequestReopened

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestReopened |
| Selected source event name | AuditorRequestReopened |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestReopened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestReopened |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-request-reopened","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestReopened.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A resolved request was reopened as the root native fact of its operation. The request generation advances and Change is the AuditorRequestReopenContent. |
| Selected source meaning | A resolved request was reopened as the root native fact of its operation. The request generation advances and Change is the AuditorRequestReopenContent. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestReopened.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestReopened |
| Schema discriminator | bdgrz.auditorrequests.auditor-request-reopened |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorRequestReopened.cs |
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

#### Content schema: AuditorRequestReopened.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorRequestReopened.change.v1 |
| sourceCommandVariants | ReopenAuditorRequest |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorRequestResolutionAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestResolutionAccepted |
| Selected source event name | AuditorRequestResolutionAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestResolutionAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestResolutionAccepted |
| Adopted wire | `{"discriminator":"bdgrz.auditor-request.auditor-request-resolution-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolutionAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The root native acceptance of a resolution. It seals response, comment and sample admission for Generation and references the frozen ClosureIntent; the request only resolves once AuditorRequestResolved follows. |
| Selected source meaning | The root native acceptance of a resolution. It seals response, comment and sample admission for Generation and references the frozen ClosureIntent; the request only resolves once AuditorRequestResolved follows. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolutionAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestResolutionAccepted |
| Schema discriminator | bdgrz.auditor-request.auditor-request-resolution-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
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
| [T035](../reactors/auditor-request-resolution-reactor.md#t035) | physical root, logical prerequisite | AuditorRequest | CompleteAuditorRequestResolution | AuditorRequest |
| [T061](../reactors/auditor-request-resolution-reactor.md#t061) | physical root, logical prerequisite | AuditorRequest | AuthorizeAuditorResponseRequestResolution | AuditorResponse |
| [T062](../reactors/auditor-request-resolution-reactor.md#t062) | physical root | AuditorRequest | CloseAuditorResponseRequestResolutionAuthorization | AuditorResponse |
| [A007](../reactors/unassigned-architecture-rows.md#a007) | physical root, logical prerequisite | AuditorRequest | AbortAuditorRequestResolution | AuditorRequest |

### AuditorRequestResolutionCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestResolutionCanceled |
| Selected source event name | AuditorRequestResolutionCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestResolutionCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestResolutionCanceled |
| Adopted wire | `{"discriminator":"bdgrz.auditor-request.auditor-request-resolution-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolutionCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A pending resolution ended before it resolved the request. The fact is the local receiver fence: a delayed CompleteAuditorRequestResolution sees it and does nothing, and only then are the response uses settled. SourceProofs is the failure proof. Admission reopens for the generation. |
| Selected source meaning | A pending resolution ended before it resolved the request. The fact is the local receiver fence: a delayed CompleteAuditorRequestResolution sees it and does nothing, and only then are the response uses settled. SourceProofs is the failure proof. Admission reopens for the generation. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolutionCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestResolutionCanceled |
| Schema discriminator | bdgrz.auditor-request.auditor-request-resolution-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
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
| [T062](../reactors/auditor-request-resolution-reactor.md#t062) | logical prerequisite | AuditorRequest | CloseAuditorResponseRequestResolutionAuthorization | AuditorResponse |
| [A007](../reactors/unassigned-architecture-rows.md#a007) | emitted semantic fact | AuditorRequest | AbortAuditorRequestResolution | AuditorRequest |

### AuditorRequestResolved

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestResolved |
| Selected source event name | AuditorRequestResolved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestResolved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestResolved |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-request-resolved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The request was resolved by the exact pending resolution. Change is the frozen ClosureIntent of the resolution, ResponseDispositionProofs the complete manifest of the held accepted dispositions of every admitted response and RequestGeneration the generation the resolution sealed. |
| Selected source meaning | The request was resolved by the exact pending resolution. Change is the frozen ClosureIntent of the resolution, ResponseDispositionProofs the complete manifest of the held accepted dispositions of every admitted response and RequestGeneration the generation the resolution sealed. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestResolved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestResolved |
| Schema discriminator | bdgrz.auditorrequests.auditor-request-resolved |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorRequestResolved.cs |
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
| RequestGeneration | long | true |
| ResponseDispositionProofs | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |
| sourceCommandVariants | CompleteAuditorRequestResolution |

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
| [T035](../reactors/auditor-request-resolution-reactor.md#t035) | emitted semantic fact | AuditorRequest | CompleteAuditorRequestResolution | AuditorRequest |
| [T062](../reactors/auditor-request-resolution-reactor.md#t062) | logical prerequisite | AuditorRequest | CloseAuditorResponseRequestResolutionAuthorization | AuditorResponse |

### AuditorRequestRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorRequestRevised |
| Selected source event name | AuditorRequestRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorRequestRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestRevised |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-request-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestRevised.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The wording, scope, priority or due date of an open request was revised. Change is the complete AuditorRequestTermsContent after the revision. |
| Selected source meaning | The wording, scope, priority or due date of an open request was revised. Change is the complete AuditorRequestTermsContent after the revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestRevised |
| Schema discriminator | bdgrz.auditorrequests.auditor-request-revised |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorRequestRevised.cs |
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

#### Content schema: AuditorRequestRevised.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditorRequestRevised.change.v1 |
| sourceCommandVariants | ReviseAuditorRequest |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditorResponseAdmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorResponseAdmitted |
| Selected source event name | AuditorResponseAdmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorResponseAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorResponseAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.auditor-request.auditor-response-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The open request accepted one response draft for its current generation. Intent is the frozen ChildAdmissionIntent that carries the complete draft; the response owner is materialized independently, and a later resolution drains it instead of discarding it. The logical id of Operation is the response id. |
| Selected source meaning | The open request accepted one response draft for its current generation. Intent is the frozen ChildAdmissionIntent that carries the complete draft; the response owner is materialized independently, and a later resolution drains it instead of discarding it. The logical id of Operation is the response id. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorResponseAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorResponseAdmitted |
| Schema discriminator | bdgrz.auditor-request.auditor-response-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
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

#### Content schema: child-admission-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | child-admission-intent.v1 |
| recordType | ChildAdmissionIntent |
| rule | Concrete child payload schemas are the corresponding normalized Common requests: advisor feedback, auditor response/comment, corrective action, policy acknowledgement/training/waiver. Preserve all authored fields and original actor; child reducer never loads live parent. No later close/revoke discards admitted work. |

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
| [T033](../reactors/auditor-response-materialization-reactor.md#t033) | physical root, logical prerequisite | AuditorRequest | CreateAuditorResponseDraft | AuditorResponse |

### AuditorSampleSelectionAdmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AuditorSampleSelectionAdmitted |
| Selected source event name | AuditorSampleSelectionAdmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AuditorSampleSelectionAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorSampleSelectionAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.auditor-request.auditor-sample-selection-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorSampleSelectionAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The open request accepted one exact sample selection. Intent is the frozen AuditorSampleSelectionIntent with the complete population version content and the nonempty duplicate-free row selection. The population, its version and the superseded selection are repeated as facts so the request can enforce amendment lineage from its own ordered stream. The logical id of Operation is the selection id. |
| Selected source meaning | The open request accepted one exact sample selection. Intent is the frozen AuditorSampleSelectionIntent with the complete population version content and the nonempty duplicate-free row selection. The population, its version and the superseded selection are repeated as facts so the request can enforce amendment lineage from its own ordered stream. The logical id of Operation is the selection id. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/AuditorSampleSelectionAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorSampleSelectionAdmitted |
| Schema discriminator | bdgrz.auditor-request.auditor-sample-selection-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorRequest |
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

#### Content schema: auditor-sample-selection-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | auditor-sample-selection-intent.v1 |
| recordType | AuditorSampleSelectionIntent |
| rule | Open request admits exact nonempty duplicate-free row selection and frozen population content. Amendment identifies a prior admitted selection of the same population with strictly later population version. Freeze request revision separately from new selection zero. Resolution drains every admitted selection; later closure cannot discard it. Child materializes the exact admitted selection without a live parent read. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RequestId | Uuid | true |
| RequestGeneration | ulong | true |
| ParentAdmissionRevision | ulong | true |
| SelectionId | Uuid | true |
| SupersedesSelectionId | Uuid? | true |
| PopulationId | Uuid | true |
| PopulationVersion | int | true |
| PopulationContent | ImmutableContentRef | true |
| PopulationRowIds | ImmutableSelectionManifest | true |
| SelectionMethod | string | true |
| Source | string | true |
| ExternalReference | string | true |
| AmendmentReason | string? | true |
| RecordedAt | DateTimeOffset | true |

##### Variant: AuditorSampleSelectionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RequestId | Uuid | true |
| RequestGeneration | ulong | true |
| ParentAdmissionRevision | ulong | true |
| SelectionId | Uuid | true |
| SupersedesSelectionId | Uuid? | true |
| PopulationId | Uuid | true |
| PopulationVersion | int | true |
| PopulationContent | ImmutableContentRef | true |
| PopulationRowIds | ImmutableSelectionManifest | true |
| SelectionMethod | string | true |
| Source | string | true |
| ExternalReference | string | true |
| AmendmentReason | string? | true |
| RecordedAt | DateTimeOffset | true |


#### Resolved intent schema

AuditorSampleSelectionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T105](../reactors/auditor-sample-selection-materialization-reactor.md#t105) | physical root, logical prerequisite | AuditorRequest | RecordAuditorSampleSelection | AuditorSampleSelection |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
