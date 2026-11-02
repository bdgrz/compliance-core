# Provider events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/provider.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ProviderChangeAccepted | business | bdgrz.provider.provider-change-accepted | 1 | 8 | The provider record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| ProviderChangeCanceled | business | bdgrz.provider.provider-change-canceled | 1 | 8 | The pending provider change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| ProviderRecorded | business | bdgrz.provider.recorded | 1 | 10 | The creating change of a provider was applied after its governed name was claimed. The complete normalized ProviderContent is the content behind ContentContent. |
| ProviderRetired | business | null (recorded) | null (recorded) | [Placeholder: schema absent] | A provider was retired from the tenant register; it can no longer be referenced by new scope, flows, or assurance reviews, and its governed name is released. |
| ProviderRevised | business | bdgrz.provider.revised | 1 | 11 | A later change of a provider was applied after the names it needs were claimed. The complete normalized ProviderContent is the content behind ContentContent. |

## Event details

### ProviderChangeAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderChangeAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderChangeAccepted |
| Adopted wire | `{"discriminator":"bdgrz.provider.provider-change-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderChangeAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The provider record accepted a complete change: its normalized content and claim keys are frozen in the intent and the name-change process continues from this fact. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderChangeAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderChangeAccepted |
| Schema discriminator | bdgrz.provider.provider-change-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | Provider |
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
| [T022](../reactors/provider-change-accepted-reactor.md#t022) | physical root, logical prerequisite | Provider | ClaimProviderName | ProviderNameClaim |
| [T023](../reactors/provider-change-accepted-reactor.md#t023) | physical root | ProviderNameClaim | ApplyProviderChange | Provider |
| [T024](../reactors/provider-change-accepted-reactor.md#t024) | physical root | Provider | ReleaseProviderName | ProviderNameClaim |
| [A014](../reactors/unassigned-architecture-rows.md#a014) | physical root, logical prerequisite | Provider | CancelProviderChange | Provider |

### ProviderChangeCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderChangeCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderChangeCanceled |
| Adopted wire | `{"discriminator":"bdgrz.provider.provider-change-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderChangeCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The pending provider change was canceled after a definite failure; it fences the local apply before any newly acquired claim is released. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderChangeCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderChangeCanceled |
| Schema discriminator | bdgrz.provider.provider-change-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | Provider |
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
| [T024](../reactors/provider-change-accepted-reactor.md#t024) | logical prerequisite | Provider | ReleaseProviderName | ProviderNameClaim |
| [A014](../reactors/unassigned-architecture-rows.md#a014) | emitted semantic fact | Provider | CancelProviderChange | Provider |

### ProviderRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderRecorded |
| Adopted wire | `{"discriminator":"bdgrz.provider.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The creating change of a provider was applied after its governed name was claimed. The complete normalized ProviderContent is the content behind ContentContent. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderRecorded |
| Schema discriminator | bdgrz.provider.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | Provider |
| Schema existingContract | src/Compliance.Common/Features/Providers/ProviderRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured ProviderContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ProviderRecorded.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | ProviderRecorded.ContentContent.v1 |
| contentType | ProviderContent |
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
| [T023](../reactors/provider-change-accepted-reactor.md#t023) | emitted semantic fact | ProviderNameClaim | ApplyProviderChange | Provider |
| [T024](../reactors/provider-change-accepted-reactor.md#t024) | logical prerequisite | Provider | ReleaseProviderName | ProviderNameClaim |

### ProviderRetired

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderRetired |
| Adopted wire | null (recorded) |
| Wire qualification | Planned; no declared record yet. Today the transition is a Lifecycle value inside the generic revision fact. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | EV-D07 |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A provider was retired from the tenant register; it can no longer be referenced by new scope, flows, or assurance reviews, and its governed name is released. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X003](../reactors/provider-change-accepted-reactor.md#x003) | physical root, logical prerequisite | Provider | ReleaseProviderName | ProviderNameClaim |

### ProviderRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ProviderRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProviderRevised |
| Adopted wire | `{"discriminator":"bdgrz.provider.revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Providers/Events/ProviderRevised.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A later change of a provider was applied after the names it needs were claimed. The complete normalized ProviderContent is the content behind ContentContent. |
| Meaning source | record summary in src/Compliance.Common/Features/Providers/Events/ProviderRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ProviderRevised |
| Schema discriminator | bdgrz.provider.revised |
| Schema discriminatorVersion | 1 |
| Schema owner | Provider |
| Schema existingContract | src/Compliance.Common/Features/Providers/ProviderRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured ProviderContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProviderId | Uuid | true |
| RequestId | Uuid | true |
| Revision | long | true |
| ContentContent | ImmutableContentRef | true |
| Actor | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ProviderRevised.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | ProviderRevised.ContentContent.v1 |
| contentType | ProviderContent |
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
| [T023](../reactors/provider-change-accepted-reactor.md#t023) | emitted semantic fact | ProviderNameClaim | ApplyProviderChange | Provider |
| [T024](../reactors/provider-change-accepted-reactor.md#t024) | logical prerequisite | Provider | ReleaseProviderName | ProviderNameClaim |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
