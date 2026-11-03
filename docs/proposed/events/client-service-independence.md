# ClientServiceIndependence events

Canonical events: **6**. Complete canonical event and available schema records: [owner event JSON](../data/events/client-service-independence.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ClientIndependenceEvaluationAdmitted | ClientIndependenceEvaluationAdmitted | business | bdgrz.client-service-independence.client-independence-evaluation-admitted | 1 | 8 | The root native acceptance of a client independence preview on the service-epoch owner. It freezes the complete preview intent at Revision, the service epoch at admission, and does not advance the epoch. The preview itself is materialized later by ClientIndependencePreviewProcess. |
| ClientServicePersonalDecisionAuthorized | ClientServicePersonalDecisionUseAuthorized | reservation | bdgrz.client-service-independence.client-service-personal-decision-use-authorized | 1 | 8 | Holds the exact complete service history and epoch a personal decision relies on; the use does not advance the public revision, so Revision is the held revision. |
| ClientServicePersonalDecisionAuthorizationClosed | ClientServicePersonalDecisionUseSettled | reservation | bdgrz.client-service-independence.client-service-personal-decision-use-settled | 1 | 8 | The use of the client service history ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| EngagementServiceHistoryReserved | EngagementServiceEpochAuthorized | reservation | bdgrz.client-service-independence.engagement-service-epoch-authorized | 1 | 8 | The client's complete service history at its current epoch was held for an engagement acceptance or staff assignment to rely on, and while held no further services may be added. |
| EngagementServiceHistoryReservationEnded | EngagementServiceEpochSettled | reservation | bdgrz.client-service-independence.engagement-service-epoch-settled | 1 | 8 | The hold of the service epoch ended after the operation recorded its terminal fact. A settlement before any grant fences the key against late acquisition. Permanent practice commitments are never affected. |
| NonattestServiceRecorded | NonattestServiceRecorded | business | bdgrz.independence.service.recorded | 1 | 7 | One nonattest service appended to the complete history of a client. The complete NonattestServiceView is immutable content; Revision is the service epoch after this service. The service record identity is OperationId. |

## Event details

### ClientIndependenceEvaluationAdmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientIndependenceEvaluationAdmitted |
| Selected source event name | ClientIndependenceEvaluationAdmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientIndependenceEvaluationAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientIndependenceEvaluationAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.client-service-independence.client-independence-evaluation-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ClientIndependenceEvaluationAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The root native acceptance of a client independence preview on the service-epoch owner. It freezes the complete preview intent at Revision, the service epoch at admission, and does not advance the epoch. The preview itself is materialized later by ClientIndependencePreviewProcess. |
| Selected source meaning | The root native acceptance of a client independence preview on the service-epoch owner. It freezes the complete preview intent at Revision, the service epoch at admission, and does not advance the epoch. The preview itself is materialized later by ClientIndependencePreviewProcess. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ClientIndependenceEvaluationAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ClientIndependenceEvaluationAdmitted |
| Schema discriminator | bdgrz.client-service-independence.client-independence-evaluation-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientServiceIndependence |
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

#### Content schema: client-independence-preview-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | client-independence-preview-intent.v1 |
| recordType | ClientIndependencePreviewIntent |
| rule | Freeze complete exact current service epoch and historical evaluation inputs at client admission. Child calculation reads only this snapshot with the selected evaluator version. Result is a historical preview; ProductionAcceptanceBlocked is always true. Infrastructure/missing coverage is transient; proven missing business facts become explicit findings. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EvaluationId | Uuid | true |
| ClientTenantId | Uuid | true |
| ServiceEpoch | ulong | true |
| RuleSetVersion | long | true |
| ExaminationPeriodStart | DateOnly | true |
| CompleteServices | ImmutableSelectionManifest | true |
| RuleContent | ImmutableContentRef | true |
| HistoricalPracticeFacts | ImmutableSelectionManifest | true |
| SourceCuts | ImmutableSelectionManifest | true |
| RuleVersionDigest | string | true |

##### Variant: ClientIndependencePreviewIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| EvaluationId | Uuid | true |
| ClientTenantId | Uuid | true |
| ServiceEpoch | ulong | true |
| RuleSetVersion | long | true |
| ExaminationPeriodStart | DateOnly | true |
| CompleteServices | ImmutableSelectionManifest | true |
| RuleContent | ImmutableContentRef | true |
| HistoricalPracticeFacts | ImmutableSelectionManifest | true |
| SourceCuts | ImmutableSelectionManifest | true |
| RuleVersionDigest | string | true |


#### Resolved intent schema

ClientIndependencePreviewIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T106](../reactors/client-independence-preview-reactor.md#t106) | physical root, logical prerequisite | ClientServiceIndependence | RecordClientIndependenceEvaluation | ClientIndependenceDecision |

### ClientServicePersonalDecisionAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientServicePersonalDecisionAuthorized |
| Selected source event name | ClientServicePersonalDecisionUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:ClientServicePersonalDecisionUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientServicePersonalDecisionUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.client-service-independence.client-service-personal-decision-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ClientServicePersonalDecisionUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Holds the exact complete service history and epoch a personal decision relies on; the use does not advance the public revision, so Revision is the held revision. |
| Selected source meaning | Holds the exact complete service history and epoch a personal decision relies on; the use does not advance the public revision, so Revision is the held revision. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ClientServicePersonalDecisionUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ClientServicePersonalDecisionUseAuthorized |
| Schema discriminator | bdgrz.client-service-independence.client-service-personal-decision-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientServiceIndependence |
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
| [T107](../reactors/engagement-personal-decision-reactor.md#t107) | emitted semantic fact | ServiceEngagementIndependence | AuthorizeClientServicePersonalDecision | ClientServiceIndependence |

### ClientServicePersonalDecisionAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientServicePersonalDecisionAuthorizationClosed |
| Selected source event name | ClientServicePersonalDecisionUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:ClientServicePersonalDecisionUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientServicePersonalDecisionUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.client-service-independence.client-service-personal-decision-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/ClientServicePersonalDecisionUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The use of the client service history ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Selected source meaning | The use of the client service history ended after the irreversible personal decision or after cancellation. A settlement before any grant fences the key against late acquisition. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/ClientServicePersonalDecisionUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ClientServicePersonalDecisionUseSettled |
| Schema discriminator | bdgrz.client-service-independence.client-service-personal-decision-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientServiceIndependence |
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
| [T111](../reactors/engagement-personal-decision-reactor.md#t111) | emitted semantic fact | ServiceEngagementIndependence | CloseClientServicePersonalDecisionAuthorization | ClientServiceIndependence |

### EngagementServiceHistoryReserved

| Canonical event detail | Value |
| --- | --- |
| Target event name | EngagementServiceHistoryReserved |
| Selected source event name | EngagementServiceEpochAuthorized |
| Name decision | Names the exact service-history hold while the engagement decision is made. |
| Canonical semantic key | event:EngagementServiceEpochAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementServiceEpochAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.client-service-independence.engagement-service-epoch-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementServiceEpochAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The client's complete service history at its current epoch was held for an engagement acceptance or staff assignment to rely on, and while held no further services may be added. |
| Selected source meaning | The client's complete service history at its current epoch was held for an engagement acceptance or staff assignment to rely on, and while held no further services may be added. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/EngagementServiceEpochAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementServiceEpochAuthorized |
| Schema discriminator | bdgrz.client-service-independence.engagement-service-epoch-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientServiceIndependence |
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
| [T004](../reactors/engagement-acceptance-reactor.md#t004) | emitted semantic fact | ServiceEngagementIndependence | ReserveEngagementServiceHistory | ClientServiceIndependence |

### EngagementServiceHistoryReservationEnded

| Canonical event detail | Value |
| --- | --- |
| Target event name | EngagementServiceHistoryReservationEnded |
| Selected source event name | EngagementServiceEpochSettled |
| Name decision | Names the end of the temporary service-history hold after decision or cancellation. |
| Canonical semantic key | event:EngagementServiceEpochSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementServiceEpochSettled |
| Adopted wire | `{"discriminator":"bdgrz.client-service-independence.engagement-service-epoch-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/EngagementServiceEpochSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The hold of the service epoch ended after the operation recorded its terminal fact. A settlement before any grant fences the key against late acquisition. Permanent practice commitments are never affected. |
| Selected source meaning | The hold of the service epoch ended after the operation recorded its terminal fact. A settlement before any grant fences the key against late acquisition. Permanent practice commitments are never affected. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/EngagementServiceEpochSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EngagementServiceEpochSettled |
| Schema discriminator | bdgrz.client-service-independence.engagement-service-epoch-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientServiceIndependence |
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
| [T007](../reactors/engagement-acceptance-reactor.md#t007) | emitted semantic fact | ServiceEngagementIndependence | ReleaseEngagementServiceHistoryReservation | ClientServiceIndependence |

### NonattestServiceRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | NonattestServiceRecorded |
| Selected source event name | NonattestServiceRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:NonattestServiceRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | NonattestServiceRecorded |
| Adopted wire | `{"discriminator":"bdgrz.independence.service.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/NonattestServiceRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | One nonattest service appended to the complete history of a client. The complete NonattestServiceView is immutable content; Revision is the service epoch after this service. The service record identity is OperationId. |
| Selected source meaning | One nonattest service appended to the complete history of a client. The complete NonattestServiceView is immutable content; Revision is the service epoch after this service. The service record identity is OperationId. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/NonattestServiceRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | NonattestServiceRecorded |
| Schema discriminator | bdgrz.independence.service.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ClientServiceIndependence |
| Schema existingContract | src/Compliance.Common/Features/AccessControl/NonattestServiceRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured NonattestServiceView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Remove former shared-ledger ExpectedSequence. Operation provenance retains authored concurrency; Revision names this new owner logical result revision. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationId | Uuid | true |
| ServiceContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Revision | ulong | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: NonattestServiceRecorded.ServiceContent.v1

| Property | Value |
| --- | --- |
| field | ServiceContent |
| schema | NonattestServiceRecorded.ServiceContent.v1 |
| contentType | NonattestServiceView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
