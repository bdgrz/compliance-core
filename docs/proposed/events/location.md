# Location events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/location.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| LocationChangeAccepted | LocationChangeAccepted | business | bdgrz.location.location-change-accepted | 1 | 8 | The location record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| LocationChangeCanceled | LocationChangeCanceled | business | bdgrz.location.location-change-canceled | 1 | 8 | The pending location change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| LocationRevisionRecorded | LocationRevisionRecorded | business | bdgrz.inventory.location.revision_recorded | 1 | 10 | Applies one complete, attributed location change after its active name was claimed; revision 1 creates it. The complete normalized LocationContent is the content behind ContentContent. |

## Event details

### LocationChangeAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | LocationChangeAccepted |
| Selected source event name | LocationChangeAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:LocationChangeAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | LocationChangeAccepted |
| Adopted wire | `{"discriminator":"bdgrz.location.location-change-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/LocationChangeAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The location record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| Selected source meaning | The location record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/LocationChangeAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | LocationChangeAccepted |
| Schema discriminator | bdgrz.location.location-change-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | Location |
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
| [T025](../reactors/location-change-accepted-reactor.md#t025) | physical root, logical prerequisite | Location | ClaimLocationName | LocationNameClaim |
| [T026](../reactors/location-change-accepted-reactor.md#t026) | physical root | LocationNameClaim | ApplyLocationChange | Location |
| [T027](../reactors/location-change-accepted-reactor.md#t027) | physical root | Location | ReleaseLocationName | LocationNameClaim |
| [A015](../reactors/unassigned-architecture-rows.md#a015) | physical root, logical prerequisite | Location | CancelLocationChange | Location |

### LocationChangeCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | LocationChangeCanceled |
| Selected source event name | LocationChangeCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:LocationChangeCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | LocationChangeCanceled |
| Adopted wire | `{"discriminator":"bdgrz.location.location-change-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/LocationChangeCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The pending location change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| Selected source meaning | The pending location change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/LocationChangeCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | LocationChangeCanceled |
| Schema discriminator | bdgrz.location.location-change-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | Location |
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
| [T027](../reactors/location-change-accepted-reactor.md#t027) | logical prerequisite | Location | ReleaseLocationName | LocationNameClaim |
| [A015](../reactors/unassigned-architecture-rows.md#a015) | emitted semantic fact | Location | CancelLocationChange | Location |

### LocationRevisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | LocationRevisionRecorded |
| Selected source event name | LocationRevisionRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:LocationRevisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | LocationRevisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.inventory.location.revision_recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/LocationRevisionRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Applies one complete, attributed location change after its active name was claimed; revision 1 creates it. The complete normalized LocationContent is the content behind ContentContent. |
| Selected source meaning | Applies one complete, attributed location change after its active name was claimed; revision 1 creates it. The complete normalized LocationContent is the content behind ContentContent. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/LocationRevisionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | LocationRevisionRecorded |
| Schema discriminator | bdgrz.inventory.location.revision_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | Location |
| Schema existingContract | src/Compliance.Common/Features/TechnologyInventory/LocationRevisionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured LocationContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| LocationId | Uuid | true |
| Revision | long | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| ChangedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: LocationRevisionRecorded.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | LocationRevisionRecorded.ContentContent.v1 |
| contentType | LocationContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

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
| [T026](../reactors/location-change-accepted-reactor.md#t026) | emitted semantic fact | LocationNameClaim | ApplyLocationChange | Location |
| [T027](../reactors/location-change-accepted-reactor.md#t027) | logical prerequisite | Location | ReleaseLocationName | LocationNameClaim |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
