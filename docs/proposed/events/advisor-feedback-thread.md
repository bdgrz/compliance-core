# AdvisorFeedbackThread events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/advisor-feedback-thread.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AdvisorFeedbackCorrected | business | bdgrz.advisor-feedback.corrected | 2 | 12 | A linked correction was appended to a feedback thread; the original contribution is preserved. |
| AdvisorFeedbackRecorded | business | bdgrz.advisor-feedback.recorded | 2 | 14 | The feedback thread was materialized from an admitted contribution, at thread revision one. Intent repeats the admission intent the handoff accepted; the author and instant are the original actor and the admission instant. |
| AdvisorFeedbackResolved | business | bdgrz.advisor-feedback.resolved | 1 | 9 | The client resolved a feedback thread. |
| AdvisorFeedbackResponded | business | bdgrz.advisor-feedback.responded | 2 | 10 | The client responded on a feedback thread. |

## Event details

### AdvisorFeedbackCorrected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorFeedbackCorrected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorFeedbackCorrected |
| Adopted wire | `{"discriminator":"bdgrz.advisor-feedback.corrected","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackCorrected.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-feedback.corrected","version":1}` |
| Meaning | A linked correction was appended to a feedback thread; the original contribution is preserved. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackCorrected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorFeedbackCorrected |
| Schema discriminator | bdgrz.advisor-feedback.corrected |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorFeedbackThread |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorFeedbackCorrected.cs |
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
| FeedbackId | Uuid | true |
| CorrectionId | Uuid | true |
| CorrectedBody | string | true |
| CorrectionReason | string | true |
| CorrectedBy | ActorReference | true |
| CorrectedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AdvisorFeedbackRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorFeedbackRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorFeedbackRecorded |
| Adopted wire | `{"discriminator":"bdgrz.advisor-feedback.recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-feedback.recorded","version":1}` |
| Meaning | The feedback thread was materialized from an admitted contribution, at thread revision one. Intent repeats the admission intent the handoff accepted; the author and instant are the original actor and the admission instant. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorFeedbackRecorded |
| Schema discriminator | bdgrz.advisor-feedback.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorFeedbackThread |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorFeedbackRecorded.cs |
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
| HandoffId | Uuid | true |
| FeedbackId | Uuid | true |
| RecordId | Uuid | true |
| RecordVersionId | Uuid | true |
| Kind | string | true |
| Body | string | true |
| Author | ActorReference | true |
| CreatedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
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
| [T013](../reactors/advisor-feedback-materialization-reactor.md#t013) | emitted semantic fact | AdvisorHandoff | MaterializeAdvisorFeedback | AdvisorFeedbackThread |

### AdvisorFeedbackResolved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorFeedbackResolved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorFeedbackResolved |
| Adopted wire | `{"discriminator":"bdgrz.advisor-feedback.resolved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackResolved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The client resolved a feedback thread. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackResolved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorFeedbackResolved |
| Schema discriminator | bdgrz.advisor-feedback.resolved |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorFeedbackThread |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorFeedbackResolved.cs |
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
| FeedbackId | Uuid | true |
| ResolvedBy | ActorReference | true |
| ResolvedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AdvisorFeedbackResponded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AdvisorFeedbackResponded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AdvisorFeedbackResponded |
| Adopted wire | `{"discriminator":"bdgrz.advisor-feedback.responded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackResponded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.advisor-feedback.responded","version":1}` |
| Meaning | The client responded on a feedback thread. |
| Meaning source | record summary in src/Compliance.Common/Features/AdvisorCollaboration/Events/AdvisorFeedbackResponded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AdvisorFeedbackResponded |
| Schema discriminator | bdgrz.advisor-feedback.responded |
| Schema discriminatorVersion | 1 |
| Schema owner | AdvisorFeedbackThread |
| Schema existingContract | src/Compliance.Common/Features/AdvisorCollaboration/AdvisorFeedbackResponded.cs |
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
| FeedbackId | Uuid | true |
| Response | string | true |
| RespondedBy | ActorReference | true |
| RespondedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AdvisorFeedbackCorrected** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AdvisorFeedbackRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AdvisorFeedbackResponded** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
