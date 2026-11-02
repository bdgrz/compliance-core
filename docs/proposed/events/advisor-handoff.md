# AdvisorHandoff events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/advisor-handoff.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AdvisorArtifactDownloadUseAuthorized | reservation | bdgrz.advisor-handoff.advisor-artifact-download-use-authorized | 1 | 8 | null (recorded) |
| AdvisorArtifactDownloadUseSettled | reservation | bdgrz.advisor-handoff.advisor-artifact-download-use-settled | 1 | 8 | null (recorded) |
| AdvisorFeedbackAdmitted | business | bdgrz.advisor-handoff.advisor-feedback-admitted | 1 | 8 | The handoff accepted one feedback contribution while it was ready, unrevoked and unclosed, on an exactly selected version. Intent is the frozen ChildAdmissionIntent that carries the complete contribution; the thread is materialized independently and the admission, not the materialization, is the authority. The logical id of Operation is the feedback id and Generation is the selection generation of the handoff at admission. |
| AdvisorHandoffClosed | business | bdgrz.advisor-handoff.closed | 2 | 10 | The handoff was closed; feedback admitted before the closure is still materialized. |
| AdvisorHandoffCreated | business | bdgrz.advisor-handoff.created | 2 | 12 | Opens one bounded advisor handoff. RecordVersionsManifest is the complete exact selection as verified content (AdvisorHandoffCreated.RecordVersionsManifest.v1); readers verify it before use. |
| AdvisorHandoffReadiedForReview | business | bdgrz.advisor-handoff.readied-for-review | 1 | 9 | The handoff was opened to the assigned advisor for review. |
| AdvisorHandoffRevoked | business | bdgrz.advisor-handoff.revoked | 2 | 10 | The handoff was revoked; feedback admitted before the revocation is still materialized. |
| AdvisorHandoffSelectionRevised | business | bdgrz.advisor-handoff.selection-revised | 2 | 11 | Replaces the selected versions of an open handoff and advances its selection generation. RecordVersionsManifest is the complete exact selection as verified content (AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1). |

## Event details

### AdvisorArtifactDownloadUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorArtifactDownloadUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorArtifactDownloadUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.advisor-artifact-download-use-authorized","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorArtifactDownloadUseAuthorized |
| Schema discriminator | bdgrz.advisor-handoff.advisor-artifact-download-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
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
| [T099](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t099) | emitted semantic fact | EvidenceArtifact | AuthorizeAdvisorArtifactDownloadUse | AdvisorHandoff |

### AdvisorArtifactDownloadUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorArtifactDownloadUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorArtifactDownloadUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.advisor-artifact-download-use-settled","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorArtifactDownloadUseSettled |
| Schema discriminator | bdgrz.advisor-handoff.advisor-artifact-download-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
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
| [T102](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t102) | emitted semantic fact | ArtifactDownloadGrant | SettleAdvisorArtifactDownloadUse | AdvisorHandoff |

### AdvisorFeedbackAdmitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorFeedbackAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorFeedbackAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.advisor-feedback-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The handoff accepted one feedback contribution while it was ready, unrevoked and unclosed, on an exactly selected version. Intent is the frozen ChildAdmissionIntent that carries the complete contribution; the thread is materialized independently and the admission, not the materialization, is the authority. The logical id of Operation is the feedback id and Generation is the selection generation of the handoff at admission. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorFeedbackAdmitted |
| Schema discriminator | bdgrz.advisor-handoff.advisor-feedback-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
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
| [T013](../reactors/advisor-feedback-materialization-reactor.md#t013) | physical root, logical prerequisite | AdvisorHandoff | MaterializeAdvisorFeedback | AdvisorFeedbackThread |

### AdvisorHandoffClosed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorHandoffClosed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorHandoffClosed |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.closed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffClosed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-handoff.closed","version":1}` |
| Meaning | The handoff was closed; feedback admitted before the closure is still materialized. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffClosed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorHandoffClosed |
| Schema discriminator | bdgrz.advisor-handoff.closed |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorHandoffClosed.cs |
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
| HandoffId | Uuid | true |
| Revision | long | true |
| Rationale | string | true |
| ClosedBy | ActorReference | true |
| ClosedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AdvisorHandoffCreated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorHandoffCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorHandoffCreated |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.created","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffCreated.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-handoff.created","version":1}` |
| Meaning | Opens one bounded advisor handoff. RecordVersionsManifest is the complete exact selection as verified content (AdvisorHandoffCreated.RecordVersionsManifest.v1); readers verify it before use. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffCreated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorHandoffCreated |
| Schema discriminator | bdgrz.advisor-handoff.created |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorHandoffCreated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<AdvisorRecordVersion> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| HandoffId | Uuid | true |
| RecordVersionsManifest | ImmutableContentRef | true |
| Purpose | string | true |
| ExpiresOn | DateOnly | true |
| CreatedBy | ActorReference | true |
| CreatedAt | DateTimeOffset | true |
| ComparedToHandoffId | Uuid? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AdvisorHandoffCreated.RecordVersionsManifest.v1

| Property | Value |
| --- | --- |
| field | RecordVersionsManifest |
| schema | AdvisorHandoffCreated.RecordVersionsManifest.v1 |
| contentType | IReadOnlyList<AdvisorRecordVersion> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AdvisorHandoffReadiedForReview

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorHandoffReadiedForReview |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorHandoffReadiedForReview |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.readied-for-review","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffReadiedForReview.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The handoff was opened to the assigned advisor for review. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffReadiedForReview.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorHandoffReadiedForReview |
| Schema discriminator | bdgrz.advisor-handoff.readied-for-review |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorHandoffReadiedForReview.cs |
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
| HandoffId | Uuid | true |
| Revision | long | true |
| ReadiedBy | ActorReference | true |
| ReadiedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AdvisorHandoffRevoked

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorHandoffRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorHandoffRevoked |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.revoked","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffRevoked.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-handoff.revoked","version":1}` |
| Meaning | The handoff was revoked; feedback admitted before the revocation is still materialized. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffRevoked.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorHandoffRevoked |
| Schema discriminator | bdgrz.advisor-handoff.revoked |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorHandoffRevoked.cs |
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
| HandoffId | Uuid | true |
| Revision | long | true |
| Rationale | string | true |
| RevokedBy | ActorReference | true |
| RevokedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AdvisorHandoffSelectionRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorHandoffSelectionRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorHandoffSelectionRevised |
| Adopted wire | `{"discriminator":"bdgrz.advisor-handoff.selection-revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffSelectionRevised.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-handoff.selection-revised","version":1}` |
| Meaning | Replaces the selected versions of an open handoff and advances its selection generation. RecordVersionsManifest is the complete exact selection as verified content (AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1). |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorHandoffSelectionRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorHandoffSelectionRevised |
| Schema discriminator | bdgrz.advisor-handoff.selection-revised |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorHandoff |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorHandoffSelectionRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<AdvisorRecordVersion> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| HandoffId | Uuid | true |
| Revision | long | true |
| RecordVersionsManifest | ImmutableContentRef | true |
| ChangeReason | string | true |
| RevisedBy | ActorReference | true |
| RevisedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1

| Property | Value |
| --- | --- |
| field | RecordVersionsManifest |
| schema | AdvisorHandoffSelectionRevised.RecordVersionsManifest.v1 |
| contentType | IReadOnlyList<AdvisorRecordVersion> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AdvisorHandoffClosed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AdvisorHandoffCreated** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AdvisorHandoffRevoked** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AdvisorHandoffSelectionRevised** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
