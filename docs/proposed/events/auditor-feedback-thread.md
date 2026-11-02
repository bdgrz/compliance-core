# AuditorFeedbackThread events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/auditor-feedback-thread.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditorRequestCommentRecorded | business | bdgrz.auditorrequests.auditor-request-comment-recorded | 1 | 9 | An admitted auditor request comment became its own feedback thread, recorded as the thread's first revision with the commenting member and time. |

## Event details

### AuditorRequestCommentRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditorRequestCommentRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditorRequestCommentRecorded |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.auditor-request-comment-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestCommentRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An admitted auditor request comment became its own feedback thread, recorded as the thread's first revision with the commenting member and time. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditorRequests/Events/AuditorRequestCommentRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditorRequestCommentRecorded |
| Schema discriminator | bdgrz.auditorrequests.auditor-request-comment-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorFeedbackThread |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/AuditorRequestCommentRecorded.cs |
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
| sourceCommandVariants | MaterializeAuditorComment |

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
| [T034](../reactors/auditor-comment-materialization-reactor.md#t034) | emitted semantic fact | AuditorRequest | MaterializeAuditorComment | AuditorFeedbackThread |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
