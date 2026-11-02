# PolicyCampaignParticipant events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/policy-campaign-participant.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| CampaignWaiverApproved | business | bdgrz.policy_campaign.waiver.approved | 2 | 13 | A bounded exception that counts the person as excepted until it expires, applied to the participant owner. |
| PolicyAcknowledgementRecorded | business | bdgrz.policy.acknowledgement.recorded | 2 | 16 | A person's acknowledgement of the exact policy version, hash, and text shown, applied to the participant owner of (CampaignId, PersonId). Performer is the workforce person; Recorder is the member who recorded it, which differs only when a member records it for a non-member. |
| PolicyCampaignParticipantCampaignClosureUseAuthorized | reservation | bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-authorized | 1 | 8 | The participant's applied results are held for the campaign closure. The use does not advance the public participant revision; Revision is the held revision. |
| PolicyCampaignParticipantCampaignClosureUseSettled | reservation | bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-settled | 1 | 8 | The participant's closure use ended with the campaign's terminal fact. A settlement before any grant fences the key, so a delayed acquisition is refused. |
| TrainingCompletionRecorded | business | bdgrz.training_completion.recorded | 2 | 15 | A reviewed completion observation from manual evidence or an lms_export row, applied to the participant owner. The evidence reference names the source artifact. |

## Event details

### CampaignWaiverApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:CampaignWaiverApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CampaignWaiverApproved |
| Adopted wire | `{"discriminator":"bdgrz.policy_campaign.waiver.approved","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/CampaignWaiverApproved.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.policy_campaign.waiver.approved","version":1}` |
| Meaning | A bounded exception that counts the person as excepted until it expires, applied to the participant owner. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/CampaignWaiverApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CampaignWaiverApproved |
| Schema discriminator | bdgrz.policy_campaign.waiver.approved |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyCampaignParticipant |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/CampaignWaiverApproved.cs |
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
| CampaignId | Uuid | true |
| WaiverId | Uuid | true |
| PersonId | Uuid | true |
| Reason | string | true |
| ExpiresOn | DateOnly | true |
| Approver | ActorReference | true |
| ApproverMemberId | Uuid | true |
| ApprovedAt | DateTimeOffset | true |
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
| [T076](../reactors/policy-participant-result-reactor.md#t076) | emitted semantic fact | PolicyDistributionCampaign | ApplyPolicyParticipantResult | PolicyCampaignParticipant |

### PolicyAcknowledgementRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyAcknowledgementRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyAcknowledgementRecorded |
| Adopted wire | `{"discriminator":"bdgrz.policy.acknowledgement.recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyAcknowledgementRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.policy.acknowledgement.recorded","version":1}` |
| Meaning | A person's acknowledgement of the exact policy version, hash, and text shown, applied to the participant owner of (CampaignId, PersonId). Performer is the workforce person; Recorder is the member who recorded it, which differs only when a member records it for a non-member. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyAcknowledgementRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyAcknowledgementRecorded |
| Schema discriminator | bdgrz.policy.acknowledgement.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyCampaignParticipant |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/PolicyAcknowledgementRecorded.cs |
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
| CampaignId | Uuid | true |
| AcknowledgementId | Uuid | true |
| PersonId | Uuid | true |
| PolicyId | Uuid | true |
| PolicyVersion | long | true |
| ContentSha256 | string | true |
| AcknowledgementText | string | true |
| Performer | ActorReference | true |
| Recorder | ActorReference | true |
| RecordedOnBehalf | bool | true |
| AcknowledgedAt | DateTimeOffset | true |
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
| [T076](../reactors/policy-participant-result-reactor.md#t076) | emitted semantic fact | PolicyDistributionCampaign | ApplyPolicyParticipantResult | PolicyCampaignParticipant |

### PolicyCampaignParticipantCampaignClosureUseAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignParticipantCampaignClosureUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignParticipantCampaignClosureUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignParticipantCampaignClosureUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The participant's applied results are held for the campaign closure. The use does not advance the public participant revision; Revision is the held revision. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignParticipantCampaignClosureUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignParticipantCampaignClosureUseAuthorized |
| Schema discriminator | bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyCampaignParticipant |
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
| [T067](../reactors/policy-campaign-closure-reactor.md#t067) | emitted semantic fact | PolicyDistributionCampaign | AuthorizePolicyCampaignParticipantCampaignClosureUse | PolicyCampaignParticipant |

### PolicyCampaignParticipantCampaignClosureUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PolicyCampaignParticipantCampaignClosureUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PolicyCampaignParticipantCampaignClosureUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignParticipantCampaignClosureUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The participant's closure use ended with the campaign's terminal fact. A settlement before any grant fences the key, so a delayed acquisition is refused. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/PolicyCampaignParticipantCampaignClosureUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PolicyCampaignParticipantCampaignClosureUseSettled |
| Schema discriminator | bdgrz.policy-campaign-participant.policy-campaign-participant-campaign-closure-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyCampaignParticipant |
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
| [T068](../reactors/policy-campaign-closure-reactor.md#t068) | emitted semantic fact | PolicyDistributionCampaign | SettlePolicyCampaignParticipantCampaignClosureUse | PolicyCampaignParticipant |

### TrainingCompletionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TrainingCompletionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TrainingCompletionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.training_completion.recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/TrainingCompletionRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.training_completion.recorded","version":1}` |
| Meaning | A reviewed completion observation from manual evidence or an lms_export row, applied to the participant owner. The evidence reference names the source artifact. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/TrainingCompletionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TrainingCompletionRecorded |
| Schema discriminator | bdgrz.training_completion.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | PolicyCampaignParticipant |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/TrainingCompletionRecorded.cs |
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
| CampaignId | Uuid | true |
| CompletionId | Uuid | true |
| PersonId | Uuid | true |
| RequirementId | Uuid | true |
| RequirementVersion | long | true |
| CompletedOn | DateOnly | true |
| Source | string | true |
| EvidenceReference | string | true |
| Recorder | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
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
| [T076](../reactors/policy-participant-result-reactor.md#t076) | emitted semantic fact | PolicyDistributionCampaign | ApplyPolicyParticipantResult | PolicyCampaignParticipant |


## Wire/schema version discrepancies

- **CampaignWaiverApproved** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **PolicyAcknowledgementRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TrainingCompletionRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
