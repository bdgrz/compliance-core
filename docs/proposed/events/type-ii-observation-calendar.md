# TypeIIObservationCalendar events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/type-ii-observation-calendar.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| TypeIIPeriodRangeAuthorized | reservation | bdgrz.type-ii-observation-calendar.type-ii-period-range-authorized | 2 | 8 | The calendar holds the new range of one opening or amendment. The calendar's own reservation generation is the number of authorizations up to and including this fact. PriorOperationId names the live reservation an amendment replaces; that reservation stays held until the amendment is applied. The range and period remain on the native fact; Header and CurrentAuthority bind the compact live authority and linked terminal history. |
| TypeIIPeriodRangeSettled | reservation | bdgrz.type-ii-observation-calendar.type-ii-period-range-settled | 2 | 8 | The calendar settled exactly one reservation, TargetOperationId, and for an applied amendment also released the replaced ReleasedOperationId. A settlement before any authorization fences the reservation so a late acquisition is refused. |

## Event details

### TypeIIPeriodRangeAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodRangeAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodRangeAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.type-ii-observation-calendar.type-ii-period-range-authorized","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodRangeAuthorized.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.type-i-i-observation-calendar.type-i-i-period-range-authorized","version":1}` |
| Meaning | The calendar holds the new range of one opening or amendment. The calendar's own reservation generation is the number of authorizations up to and including this fact. PriorOperationId names the live reservation an amendment replaces; that reservation stays held until the amendment is applied. The range and period remain on the native fact; Header and CurrentAuthority bind the compact live authority and linked terminal history. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodRangeAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIIPeriodRangeAuthorized |
| Schema discriminator | bdgrz.type-i-i-observation-calendar.type-i-i-period-range-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIObservationCalendar |
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

#### Content schema: type-i-i-range-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | type-i-i-range-intent.v1 |
| recordType | TypeIIRangeIntent |
| rule | Persist before calendar write; opening has no prior grant. Amendments reserve old+new with exact prior grant; canceled amendment releases only pending new grant, never old active range. Source close freezes exact current grant/event/token. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| PeriodId | Uuid | true |
| OperationKind | TypeIIRangeOperationKind | true |
| PriorGrant | ReservationGrant? | true |
| OldRange | ObservationRange? | true |
| NewRange | ObservationRange | true |
| OpeningOrAmendmentContent | ImmutableContentRef | true |
| NewReservationKey | ReservationKey | true |

##### Variant: TypeIIRangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| PeriodId | Uuid | true |
| OperationKind | TypeIIRangeOperationKind | true |
| PriorGrant | ReservationGrant? | true |
| OldRange | ObservationRange? | true |
| NewRange | ObservationRange | true |
| OpeningOrAmendmentContent | ImmutableContentRef | true |
| NewReservationKey | ReservationKey | true |


#### Resolved intent schema

TypeIIRangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T053](../reactors/type-ii-range-reactor.md#t053) | emitted semantic fact | TypeIIPeriod | AcquireTypeIIPeriodRange | TypeIIObservationCalendar |
| [T054](../reactors/type-ii-range-reactor.md#t054) | logical prerequisite | TypeIIObservationCalendar | ApplyTypeIIPeriodRange | TypeIIPeriod |

### TypeIIPeriodRangeSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodRangeSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodRangeSettled |
| Adopted wire | `{"discriminator":"bdgrz.type-ii-observation-calendar.type-ii-period-range-settled","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodRangeSettled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.type-i-i-observation-calendar.type-i-i-period-range-settled","version":1}` |
| Meaning | The calendar settled exactly one reservation, TargetOperationId, and for an applied amendment also released the replaced ReleasedOperationId. A settlement before any authorization fences the reservation so a late acquisition is refused. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodRangeSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIIPeriodRangeSettled |
| Schema discriminator | bdgrz.type-i-i-observation-calendar.type-i-i-period-range-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIObservationCalendar |
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

#### Content schema: type-i-i-range-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | type-i-i-range-intent.v1 |
| recordType | TypeIIRangeIntent |
| rule | Persist before calendar write; opening has no prior grant. Amendments reserve old+new with exact prior grant; canceled amendment releases only pending new grant, never old active range. Source close freezes exact current grant/event/token. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| PeriodId | Uuid | true |
| OperationKind | TypeIIRangeOperationKind | true |
| PriorGrant | ReservationGrant? | true |
| OldRange | ObservationRange? | true |
| NewRange | ObservationRange | true |
| OpeningOrAmendmentContent | ImmutableContentRef | true |
| NewReservationKey | ReservationKey | true |

##### Variant: TypeIIRangeIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| PeriodId | Uuid | true |
| OperationKind | TypeIIRangeOperationKind | true |
| PriorGrant | ReservationGrant? | true |
| OldRange | ObservationRange? | true |
| NewRange | ObservationRange | true |
| OpeningOrAmendmentContent | ImmutableContentRef | true |
| NewReservationKey | ReservationKey | true |


#### Resolved intent schema

TypeIIRangeIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T055](../reactors/type-ii-range-reactor.md#t055) | emitted semantic fact | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |


## Wire/schema version discrepancies

- **TypeIIPeriodRangeAuthorized** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **TypeIIPeriodRangeSettled** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
