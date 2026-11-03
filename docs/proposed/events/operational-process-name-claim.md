# OperationalProcessNameClaim events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/operational-process-name-claim.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| OperationalProcessNameClaimed | OperationalProcessNameClaimed | business | bdgrz.operational-process-name-claim.operational-process-name-claimed | 1 | 8 | The name claim is held by the operational process record and change generation named by this fact. |
| OperationalProcessNameReleased | OperationalProcessNameReleased | business | bdgrz.operational-process-name-claim.operational-process-name-released | 1 | 8 | The name claim was released by the operational process record and change generation that held it. |

## Event details

### OperationalProcessNameClaimed

| Canonical event detail | Value |
| --- | --- |
| Target event name | OperationalProcessNameClaimed |
| Selected source event name | OperationalProcessNameClaimed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:OperationalProcessNameClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | OperationalProcessNameClaimed |
| Adopted wire | `{"discriminator":"bdgrz.operational-process-name-claim.operational-process-name-claimed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessNameClaimed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The name claim is held by the operational process record and change generation named by this fact. |
| Selected source meaning | The name claim is held by the operational process record and change generation named by this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessNameClaimed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | OperationalProcessNameClaimed |
| Schema discriminator | bdgrz.operational-process-name-claim.operational-process-name-claimed |
| Schema discriminatorVersion | 1 |
| Schema owner | OperationalProcessNameClaim |
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

#### Content schema: record-name-change-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | record-name-change-intent.v1 |
| recordType | RecordNameChangeIntent |
| rule | Freeze complete normalized provider/location/process content with exact existing kind/name equality rules. Every required claim key known before acquisition. Keep old claim through successful apply. Cancellation carries this original intent and fences local apply before releasing new claim. Provider has no lifecycle field: null means unsupported, and its name stays claimed until rename. Location uses LocationContent and same-kind active-name rules; process uses OperationalProcessContent; provider uses ProviderContent. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RecordKind | NamedRecordKind | true |
| RecordId | Uuid | true |
| ExpectedRecordRevision | ulong | true |
| OldClaimKey | EventStreamAddress? | true |
| NewClaimKey | EventStreamAddress? | true |
| DesiredLifecycle | string? | true |
| AuthoredContent | ImmutableContentRef | true |
| CanonicalNormalizationVersion | string | true |

##### Variant: RecordNameChangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RecordKind | NamedRecordKind | true |
| RecordId | Uuid | true |
| ExpectedRecordRevision | ulong | true |
| OldClaimKey | EventStreamAddress? | true |
| NewClaimKey | EventStreamAddress? | true |
| DesiredLifecycle | string? | true |
| AuthoredContent | ImmutableContentRef | true |
| CanonicalNormalizationVersion | string | true |


#### Resolved intent schema

RecordNameChangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T028](../reactors/operational-process-change-accepted-reactor.md#t028) | emitted semantic fact | OperationalProcess | ClaimOperationalProcessName | OperationalProcessNameClaim |
| [T029](../reactors/operational-process-change-accepted-reactor.md#t029) | logical prerequisite | OperationalProcessNameClaim | ApplyOperationalProcessChange | OperationalProcess |

### OperationalProcessNameReleased

| Canonical event detail | Value |
| --- | --- |
| Target event name | OperationalProcessNameReleased |
| Selected source event name | OperationalProcessNameReleased |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:OperationalProcessNameReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | OperationalProcessNameReleased |
| Adopted wire | `{"discriminator":"bdgrz.operational-process-name-claim.operational-process-name-released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessNameReleased.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The name claim was released by the operational process record and change generation that held it. |
| Selected source meaning | The name claim was released by the operational process record and change generation that held it. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessNameReleased.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | OperationalProcessNameReleased |
| Schema discriminator | bdgrz.operational-process-name-claim.operational-process-name-released |
| Schema discriminatorVersion | 1 |
| Schema owner | OperationalProcessNameClaim |
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

#### Content schema: record-name-change-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | record-name-change-intent.v1 |
| recordType | RecordNameChangeIntent |
| rule | Freeze complete normalized provider/location/process content with exact existing kind/name equality rules. Every required claim key known before acquisition. Keep old claim through successful apply. Cancellation carries this original intent and fences local apply before releasing new claim. Provider has no lifecycle field: null means unsupported, and its name stays claimed until rename. Location uses LocationContent and same-kind active-name rules; process uses OperationalProcessContent; provider uses ProviderContent. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RecordKind | NamedRecordKind | true |
| RecordId | Uuid | true |
| ExpectedRecordRevision | ulong | true |
| OldClaimKey | EventStreamAddress? | true |
| NewClaimKey | EventStreamAddress? | true |
| DesiredLifecycle | string? | true |
| AuthoredContent | ImmutableContentRef | true |
| CanonicalNormalizationVersion | string | true |

##### Variant: RecordNameChangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RecordKind | NamedRecordKind | true |
| RecordId | Uuid | true |
| ExpectedRecordRevision | ulong | true |
| OldClaimKey | EventStreamAddress? | true |
| NewClaimKey | EventStreamAddress? | true |
| DesiredLifecycle | string? | true |
| AuthoredContent | ImmutableContentRef | true |
| CanonicalNormalizationVersion | string | true |


#### Resolved intent schema

RecordNameChangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T030](../reactors/operational-process-change-accepted-reactor.md#t030) | emitted semantic fact | OperationalProcess | ReleaseOperationalProcessName | OperationalProcessNameClaim |
| [X004](../reactors/operational-process-change-accepted-reactor.md#x004) | emitted semantic fact | OperationalProcess | ReleaseOperationalProcessName | OperationalProcessNameClaim |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
