# LocationNameClaim events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/location-name-claim.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| LocationNameClaimed | business | bdgrz.location-name-claim.location-name-claimed | 1 | 8 | The name claim is held by the location record and change generation named by this fact. |
| LocationNameReleased | business | bdgrz.location-name-claim.location-name-released | 1 | 8 | The name claim was released by the location record and change generation that held it. |

## Event details

### LocationNameClaimed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:LocationNameClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | LocationNameClaimed |
| Adopted wire | `{"discriminator":"bdgrz.location-name-claim.location-name-claimed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/LocationNameClaimed.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The name claim is held by the location record and change generation named by this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/LocationNameClaimed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | LocationNameClaimed |
| Schema discriminator | bdgrz.location-name-claim.location-name-claimed |
| Schema discriminatorVersion | 1 |
| Schema owner | LocationNameClaim |
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
| [T025](../reactors/location-change-accepted-reactor.md#t025) | emitted semantic fact | Location | ClaimLocationName | LocationNameClaim |
| [T026](../reactors/location-change-accepted-reactor.md#t026) | logical prerequisite | LocationNameClaim | ApplyLocationChange | Location |

### LocationNameReleased

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:LocationNameReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | LocationNameReleased |
| Adopted wire | `{"discriminator":"bdgrz.location-name-claim.location-name-released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/LocationNameReleased.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The name claim was released by the location record and change generation that held it. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/LocationNameReleased.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | LocationNameReleased |
| Schema discriminator | bdgrz.location-name-claim.location-name-released |
| Schema discriminatorVersion | 1 |
| Schema owner | LocationNameClaim |
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
| [T027](../reactors/location-change-accepted-reactor.md#t027) | emitted semantic fact | Location | ReleaseLocationName | LocationNameClaim |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
