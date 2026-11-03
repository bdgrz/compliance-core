# PracticeIndependenceCommitment events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/practice-independence-commitment.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| PracticeIndependenceCommitted | PracticeIndependenceCommitted | business | bdgrz.practice-independence-commitment.practice-independence-committed | 1 | 8 | The permanent practice wall of one subject: this staff member or canonical user serves the practice for this client for ever. No fact releases, revokes or reactivates it, whatever happens to the engagement, the staff record or the reservation that preceded it. Revision counts the permanent commitments of the subject, and the practice and engagement are carried because the owner's reducer cannot read immutable content. |
| PracticeIndependenceReservationEnded | PracticeIndependenceReservationSettled | reservation | bdgrz.practice-independence-commitment.practice-independence-reservation-settled | 1 | 8 | The temporary reservation of a practice subject ended after the operation recorded its terminal fact. A settlement before any reservation fences the key against late acquisition. A permanent commitment made by the operation is never removed by its settlement. |
| PracticeIndependenceReserved | PracticeIndependenceReserved | reservation | bdgrz.practice-independence-commitment.practice-independence-reserved | 1 | 8 | A temporary exclusive reservation of one practice subject for one acceptance or staff assignment. It grants no permanent authority: the wall exists only once PracticeIndependenceCommitted follows the irreversible authorization. Revision is the committed revision held, and the practice and engagement are carried because the owner's reducer cannot read immutable content. |

## Event details

### PracticeIndependenceCommitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | PracticeIndependenceCommitted |
| Selected source event name | PracticeIndependenceCommitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PracticeIndependenceCommitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PracticeIndependenceCommitted |
| Adopted wire | `{"discriminator":"bdgrz.practice-independence-commitment.practice-independence-committed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/PracticeIndependenceCommitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The permanent practice wall of one subject: this staff member or canonical user serves the practice for this client for ever. No fact releases, revokes or reactivates it, whatever happens to the engagement, the staff record or the reservation that preceded it. Revision counts the permanent commitments of the subject, and the practice and engagement are carried because the owner's reducer cannot read immutable content. |
| Selected source meaning | The permanent practice wall of one subject: this staff member or canonical user serves the practice for this client for ever. No fact releases, revokes or reactivates it, whatever happens to the engagement, the staff record or the reservation that preceded it. Revision counts the permanent commitments of the subject, and the practice and engagement are carried because the owner's reducer cannot read immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/PracticeIndependenceCommitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PracticeIndependenceCommitted |
| Schema discriminator | bdgrz.practice-independence-commitment.practice-independence-committed |
| Schema discriminatorVersion | 1 |
| Schema owner | PracticeIndependenceCommitment |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T005](../reactors/engagement-acceptance-reactor.md#t005) | emitted semantic fact | ServiceEngagementIndependence | CommitPracticeIndependence | PracticeIndependenceCommitment |

### PracticeIndependenceReservationEnded

| Canonical event detail | Value |
| --- | --- |
| Target event name | PracticeIndependenceReservationEnded |
| Selected source event name | PracticeIndependenceReservationSettled |
| Name decision | Names the end of the temporary reservation after commitment or cancellation. |
| Canonical semantic key | event:PracticeIndependenceReservationSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PracticeIndependenceReservationSettled |
| Adopted wire | `{"discriminator":"bdgrz.practice-independence-commitment.practice-independence-reservation-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/PracticeIndependenceReservationSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The temporary reservation of a practice subject ended after the operation recorded its terminal fact. A settlement before any reservation fences the key against late acquisition. A permanent commitment made by the operation is never removed by its settlement. |
| Selected source meaning | The temporary reservation of a practice subject ended after the operation recorded its terminal fact. A settlement before any reservation fences the key against late acquisition. A permanent commitment made by the operation is never removed by its settlement. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/PracticeIndependenceReservationSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PracticeIndependenceReservationSettled |
| Schema discriminator | bdgrz.practice-independence-commitment.practice-independence-reservation-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | PracticeIndependenceCommitment |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | emitted semantic fact | ServiceEngagementIndependence | ClosePracticeIndependenceReservation | PracticeIndependenceCommitment |

### PracticeIndependenceReserved

| Canonical event detail | Value |
| --- | --- |
| Target event name | PracticeIndependenceReserved |
| Selected source event name | PracticeIndependenceReserved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PracticeIndependenceReserved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PracticeIndependenceReserved |
| Adopted wire | `{"discriminator":"bdgrz.practice-independence-commitment.practice-independence-reserved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/PracticeIndependenceReserved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A temporary exclusive reservation of one practice subject for one acceptance or staff assignment. It grants no permanent authority: the wall exists only once PracticeIndependenceCommitted follows the irreversible authorization. Revision is the committed revision held, and the practice and engagement are carried because the owner's reducer cannot read immutable content. |
| Selected source meaning | A temporary exclusive reservation of one practice subject for one acceptance or staff assignment. It grants no permanent authority: the wall exists only once PracticeIndependenceCommitted follows the irreversible authorization. Revision is the committed revision held, and the practice and engagement are carried because the owner's reducer cannot read immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/PracticeIndependenceReserved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | PracticeIndependenceReserved |
| Schema discriminator | bdgrz.practice-independence-commitment.practice-independence-reserved |
| Schema discriminatorVersion | 1 |
| Schema owner | PracticeIndependenceCommitment |
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

#### Content schema: engagement-independence-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | engagement-independence-intent.v1 |
| recordType | EngagementIndependenceIntent |
| rule | Capture exact accepted engagement epoch plus complete staff/canonical user set. Freeze directory, partner duty, rule and ratification requirements. All source uses and temporary subject reservations precede irreversible EngagementPracticeCommitmentsAuthorized. No new permanent wall before all reservations/proofs exist. All temporary grants derive from original immutable engagement admission and are acquired by the complete canonical key manifest, with no epoch-before-practice dependency. Only irreversible authorization validates the complete current exact grant set and permits permanent commitment. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |

##### Variant: EngagementIndependenceIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EngagementId | Uuid | true |
| EngagementRevision | ulong | true |
| OperationKind | EngagementIndependenceOperationKind | true |
| ServiceEpoch | ulong | true |
| CompleteServiceHistory | ImmutableSelectionManifest | true |
| RuleProof | SourceFactLocator | true |
| RatificationProof | SourceFactLocator | true |
| PartnerDecision | SourceFactLocator | true |
| ManagementAcknowledgement | SourceFactLocator | true |
| Subjects | ImmutableSelectionManifest | true |
| SourceUses | ImmutableSelectionManifest | true |
| EffectiveFrom | DateOnly? | true |


#### Resolved intent schema

EngagementIndependenceIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T083](../reactors/engagement-acceptance-reactor.md#t083) | emitted semantic fact | ServiceEngagementIndependence | ReservePracticeIndependence | PracticeIndependenceCommitment |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
