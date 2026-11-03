# OperationalProcess events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/operational-process.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| OperationalProcessChangeAccepted | OperationalProcessChangeAccepted | business | bdgrz.operational-process.operational-process-change-accepted | 1 | 8 | The operational process record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| OperationalProcessChangeCanceled | OperationalProcessChangeCanceled | business | bdgrz.operational-process.operational-process-change-canceled | 1 | 8 | The pending operational process change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| OperationalProcessRetired | OperationalProcessRetired | business | null (recorded) | null (recorded) | [Placeholder: schema absent] | An operational process was retired from the inventory; it can no longer be cited by new boundary or description content, and its governed name is released. |
| OperationalProcessRevisionRecorded | OperationalProcessRevisionRecorded | business | bdgrz.inventory.operational_process.revision_recorded | 1 | 10 | Applies one complete, attributed operational process change after its active name was claimed; revision 1 creates it. The complete normalized OperationalProcessContent is the content behind ContentContent. |

## Event details

### OperationalProcessChangeAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | OperationalProcessChangeAccepted |
| Selected source event name | OperationalProcessChangeAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:OperationalProcessChangeAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | OperationalProcessChangeAccepted |
| Adopted wire | `{"discriminator":"bdgrz.operational-process.operational-process-change-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessChangeAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The operational process record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| Selected source meaning | The operational process record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessChangeAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | OperationalProcessChangeAccepted |
| Schema discriminator | bdgrz.operational-process.operational-process-change-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | OperationalProcess |
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
| [T028](../reactors/operational-process-change-accepted-reactor.md#t028) | physical root, logical prerequisite | OperationalProcess | ClaimOperationalProcessName | OperationalProcessNameClaim |
| [T029](../reactors/operational-process-change-accepted-reactor.md#t029) | physical root | OperationalProcessNameClaim | ApplyOperationalProcessChange | OperationalProcess |
| [T030](../reactors/operational-process-change-accepted-reactor.md#t030) | physical root | OperationalProcess | ReleaseOperationalProcessName | OperationalProcessNameClaim |
| [A016](../reactors/unassigned-architecture-rows.md#a016) | physical root, logical prerequisite | OperationalProcess | CancelOperationalProcessChange | OperationalProcess |

### OperationalProcessChangeCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | OperationalProcessChangeCanceled |
| Selected source event name | OperationalProcessChangeCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:OperationalProcessChangeCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | OperationalProcessChangeCanceled |
| Adopted wire | `{"discriminator":"bdgrz.operational-process.operational-process-change-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessChangeCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The pending operational process change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| Selected source meaning | The pending operational process change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessChangeCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | OperationalProcessChangeCanceled |
| Schema discriminator | bdgrz.operational-process.operational-process-change-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | OperationalProcess |
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
| [T030](../reactors/operational-process-change-accepted-reactor.md#t030) | logical prerequisite | OperationalProcess | ReleaseOperationalProcessName | OperationalProcessNameClaim |
| [A016](../reactors/unassigned-architecture-rows.md#a016) | emitted semantic fact | OperationalProcess | CancelOperationalProcessChange | OperationalProcess |

### OperationalProcessRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | OperationalProcessRetired |
| Selected source event name | OperationalProcessRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:OperationalProcessRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | OperationalProcessRetired |
| Adopted wire | null (recorded) |
| Wire qualification | Planned; no declared record yet. Today the transition is a Lifecycle value inside the generic revision fact. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | EV-D07 |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An operational process was retired from the inventory; it can no longer be cited by new boundary or description content, and its governed name is released. |
| Selected source meaning | An operational process was retired from the inventory; it can no longer be cited by new boundary or description content, and its governed name is released. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X004](../reactors/operational-process-change-accepted-reactor.md#x004) | physical root, logical prerequisite | OperationalProcess | ReleaseOperationalProcessName | OperationalProcessNameClaim |

### OperationalProcessRevisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | OperationalProcessRevisionRecorded |
| Selected source event name | OperationalProcessRevisionRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:OperationalProcessRevisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | OperationalProcessRevisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.inventory.operational_process.revision_recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessRevisionRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Applies one complete, attributed operational process change after its active name was claimed; revision 1 creates it. The complete normalized OperationalProcessContent is the content behind ContentContent. |
| Selected source meaning | Applies one complete, attributed operational process change after its active name was claimed; revision 1 creates it. The complete normalized OperationalProcessContent is the content behind ContentContent. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/OperationalProcessRevisionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | OperationalProcessRevisionRecorded |
| Schema discriminator | bdgrz.inventory.operational_process.revision_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | OperationalProcess |
| Schema existingContract | src/Compliance.Common/Features/TechnologyInventory/OperationalProcessRevisionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured OperationalProcessContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| OperationalProcessId | Uuid | true |
| Revision | long | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| ChangedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: OperationalProcessRevisionRecorded.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | OperationalProcessRevisionRecorded.ContentContent.v1 |
| contentType | OperationalProcessContent |
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
| [T029](../reactors/operational-process-change-accepted-reactor.md#t029) | emitted semantic fact | OperationalProcessNameClaim | ApplyOperationalProcessChange | OperationalProcess |
| [T030](../reactors/operational-process-change-accepted-reactor.md#t030) | logical prerequisite | OperationalProcess | ReleaseOperationalProcessName | OperationalProcessNameClaim |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
