# TypeIIPeriod events

Canonical events: **11**. Complete canonical event and available schema records: [owner event JSON](../data/events/type-ii-period.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| DueControlOccurrencesGenerated | business | bdgrz.operations.due-control-occurrences-generated | 1 | 13 | The recurring control occurrences falling due in a Type II period through a stated date were selected as one complete set for a generation key, so each is generated once. |
| ObservationPeriodAmended | business | bdgrz.operations.observation-period-amended | 1 | 11 | The observation period of a Type II examination was amended by a named member under an authorized calendar, recorded as the period's next revision. |
| ObservationPeriodCloseFrozen | business | bdgrz.auditpopulations.observation-period-close-frozen | 1 | 11 | The close of a Type II observation period was frozen against the exact admitted calendar intent, fixing the end of the period under examination. |
| ObservationPeriodCloseProposed | business | bdgrz.auditpopulations.observation-period-close-proposed | 4 | [Placeholder: schema absent] | Closing a Type II observation period was proposed by a named member after validation of the period, awaiting approval. |
| ObservationPeriodStarted | business | bdgrz.operations.observation-period-started | 1 | 11 | A Type II observation period was started by a named member under an authorized calendar, beginning the window over which controls are examined. |
| TypeIIPeriodAmendmentAccepted | business | bdgrz.type-ii-period.type-ii-period-amendment-accepted | 1 | 8 | The root native acceptance of a period amendment. Both the old and the new range are frozen in the intent; the range of the period only changes once ObservationPeriodAmended follows. |
| TypeIIPeriodAmendmentCanceled | business | bdgrz.type-ii-period.type-ii-period-amendment-canceled | 1 | 8 | A pending amendment ended before it applied, by explicit cancellation or by a proven calendar conflict. The fact is the local receiver fence: a delayed apply sees it and does nothing, and only then is the pending calendar reservation released. |
| TypeIIPeriodCloseAmended | business | bdgrz.auditpopulations.type-i-i-period-close-amended | 3 | [Placeholder: schema absent] | The recorded close of a Type II period was amended by a named member under the exact admitted calendar intent, replacing the prior close. |
| TypeIIPeriodCloseApproved | business | bdgrz.auditpopulations.type-i-i-period-close-approved | 3 | [Placeholder: schema absent] | The proposed close of a Type II period was approved by a named member, closing the observation period. |
| TypeIIPeriodOpeningAccepted | business | bdgrz.type-ii-period.type-ii-period-opening-accepted | 1 | 8 | The root native acceptance of a period opening. The complete opening intent is persisted here before the calendar is touched; the period only exists once ObservationPeriodStarted follows. |
| TypeIIPeriodOpeningRejected | business | bdgrz.type-ii-period.type-ii-period-opening-rejected | 1 | 8 | A pending opening was rejected because the calendar proved its range conflicts. The rejection fences a delayed apply; the same period identity is never reopened with changed intent. |

## Event details

### DueControlOccurrencesGenerated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:DueControlOccurrencesGenerated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | DueControlOccurrencesGenerated |
| Adopted wire | `{"discriminator":"bdgrz.operations.due-control-occurrences-generated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Operations/Events/DueControlOccurrencesGenerated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The recurring control occurrences falling due in a Type II period through a stated date were selected as one complete set for a generation key, so each is generated once. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Operations/Events/DueControlOccurrencesGenerated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | DueControlOccurrencesGenerated |
| Schema discriminator | bdgrz.operations.due-control-occurrences-generated |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
| Schema existingContract | src/Compliance.Common/Features/Operations/DueControlOccurrencesGenerated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | current retained/narrow owner source fact |
| Schema changes | Externalize complete IReadOnlyList<Uuid> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| GenerationKey | string | true |
| ThroughDate | DateOnly | true |
| OccurrenceIdsManifest | ImmutableContentRef | true |
| ActorMemberId | Uuid? | true |
| ActorDisplay | string | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| CreationIntentManifest | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: DueControlOccurrencesGenerated.OccurrenceIdsManifest.v1

| Property | Value |
| --- | --- |
| field | OccurrenceIdsManifest |
| schema | DueControlOccurrencesGenerated.OccurrenceIdsManifest.v1 |
| contentType | IReadOnlyList<Uuid> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T040](../reactors/control-occurrence-fan-out-reactor.md#t040) | physical root, logical prerequisite | TypeIIPeriod | MaterializeControlOccurrence | ControlOccurrence |

### ObservationPeriodAmended

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ObservationPeriodAmended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ObservationPeriodAmended |
| Adopted wire | `{"discriminator":"bdgrz.operations.observation-period-amended","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Operations/Events/ObservationPeriodAmended.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The observation period of a Type II examination was amended by a named member under an authorized calendar, recorded as the period's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Operations/Events/ObservationPeriodAmended.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ObservationPeriodAmended |
| Schema discriminator | bdgrz.operations.observation-period-amended |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
| Schema existingContract | src/Compliance.Common/Features/Operations/ObservationPeriodAmended.cs |
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
| CalendarAuthorizationId | Uuid | true |
| CalendarGeneration | long | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: type-i-i-range-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | type-i-i-range-intent.v1 |
| recordType | TypeIIRangeIntent |
| rule | Persist before calendar write; opening has no prior grant. Amendments reserve old+new with exact prior grant; canceled amendment releases only pending new grant, never old active range. Source close freezes exact current grant/event/token. |
| sourceCommandVariants | ApplyTypeIIPeriodRange |

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
| [T054](../reactors/type-ii-range-reactor.md#t054) | emitted semantic fact | TypeIIObservationCalendar | ApplyTypeIIPeriodRange | TypeIIPeriod |
| [T055](../reactors/type-ii-range-reactor.md#t055) | logical prerequisite | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |

### ObservationPeriodCloseFrozen

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ObservationPeriodCloseFrozen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ObservationPeriodCloseFrozen |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.observation-period-close-frozen","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPopulations/Events/ObservationPeriodCloseFrozen.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The close of a Type II observation period was frozen against the exact admitted calendar intent, fixing the end of the period under examination. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPopulations/Events/ObservationPeriodCloseFrozen.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ObservationPeriodCloseFrozen |
| Schema discriminator | bdgrz.auditpopulations.observation-period-close-frozen |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
| Schema existingContract | src/Compliance.Common/Features/AuditPopulations/ObservationPeriodCloseFrozen.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | current retained/narrow owner source fact |
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
| CalendarAuthorizationId | Uuid | true |
| CalendarGeneration | long | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ObservationPeriodCloseFrozen.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | ObservationPeriodCloseFrozen.change.v1 |
| sourceCommandVariants | FreezeTypeIIPeriodClose |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T055](../reactors/type-ii-range-reactor.md#t055) | physical root, logical prerequisite | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |

### ObservationPeriodCloseProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ObservationPeriodCloseProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ObservationPeriodCloseProposed |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.observation-period-close-proposed","version":4}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPopulations/Events/ObservationPeriodCloseProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Closing a Type II observation period was proposed by a named member after validation of the period, awaiting approval. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPopulations/Events/ObservationPeriodCloseProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ObservationPeriodStarted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ObservationPeriodStarted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ObservationPeriodStarted |
| Adopted wire | `{"discriminator":"bdgrz.operations.observation-period-started","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Operations/Events/ObservationPeriodStarted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A Type II observation period was started by a named member under an authorized calendar, beginning the window over which controls are examined. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Operations/Events/ObservationPeriodStarted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ObservationPeriodStarted |
| Schema discriminator | bdgrz.operations.observation-period-started |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
| Schema existingContract | src/Compliance.Common/Features/Operations/ObservationPeriodStarted.cs |
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
| CalendarAuthorizationId | Uuid | true |
| CalendarGeneration | long | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: type-i-i-range-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | type-i-i-range-intent.v1 |
| recordType | TypeIIRangeIntent |
| rule | Persist before calendar write; opening has no prior grant. Amendments reserve old+new with exact prior grant; canceled amendment releases only pending new grant, never old active range. Source close freezes exact current grant/event/token. |
| sourceCommandVariants | ApplyTypeIIPeriodRange |

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
| [T054](../reactors/type-ii-range-reactor.md#t054) | emitted semantic fact | TypeIIObservationCalendar | ApplyTypeIIPeriodRange | TypeIIPeriod |
| [T055](../reactors/type-ii-range-reactor.md#t055) | logical prerequisite | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |

### TypeIIPeriodAmendmentAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodAmendmentAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodAmendmentAccepted |
| Adopted wire | `{"discriminator":"bdgrz.type-ii-period.type-ii-period-amendment-accepted","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodAmendmentAccepted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.type-i-i-period.type-i-i-period-amendment-accepted","version":1}` |
| Meaning | The root native acceptance of a period amendment. Both the old and the new range are frozen in the intent; the range of the period only changes once ObservationPeriodAmended follows. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodAmendmentAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIIPeriodAmendmentAccepted |
| Schema discriminator | bdgrz.type-i-i-period.type-i-i-period-amendment-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
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
| [T053](../reactors/type-ii-range-reactor.md#t053) | physical root, logical prerequisite | TypeIIPeriod | AcquireTypeIIPeriodRange | TypeIIObservationCalendar |
| [T054](../reactors/type-ii-range-reactor.md#t054) | physical root | TypeIIObservationCalendar | ApplyTypeIIPeriodRange | TypeIIPeriod |
| [T055](../reactors/type-ii-range-reactor.md#t055) | physical root | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |
| [A018](../reactors/unassigned-architecture-rows.md#a018) | physical root, logical prerequisite | TypeIIPeriod | RejectTypeIIPeriodOpening | TypeIIPeriod |
| [A019](../reactors/unassigned-architecture-rows.md#a019) | physical root, logical prerequisite | TypeIIPeriod | RejectTypeIIPeriodAmendment | TypeIIPeriod |
| [A020](../reactors/unassigned-architecture-rows.md#a020) | physical root, logical prerequisite | TypeIIPeriod | CancelTypeIIPeriodAmendment | TypeIIPeriod |

### TypeIIPeriodAmendmentCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodAmendmentCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodAmendmentCanceled |
| Adopted wire | `{"discriminator":"bdgrz.type-ii-period.type-ii-period-amendment-canceled","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodAmendmentCanceled.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.type-i-i-period.type-i-i-period-amendment-canceled","version":1}` |
| Meaning | A pending amendment ended before it applied, by explicit cancellation or by a proven calendar conflict. The fact is the local receiver fence: a delayed apply sees it and does nothing, and only then is the pending calendar reservation released. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodAmendmentCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIIPeriodAmendmentCanceled |
| Schema discriminator | bdgrz.type-i-i-period.type-i-i-period-amendment-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
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
| [T055](../reactors/type-ii-range-reactor.md#t055) | physical root, logical prerequisite | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |
| [A019](../reactors/unassigned-architecture-rows.md#a019) | emitted semantic fact | TypeIIPeriod | RejectTypeIIPeriodAmendment | TypeIIPeriod |
| [A020](../reactors/unassigned-architecture-rows.md#a020) | emitted semantic fact | TypeIIPeriod | CancelTypeIIPeriodAmendment | TypeIIPeriod |

### TypeIIPeriodCloseAmended

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodCloseAmended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodCloseAmended |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.type-i-i-period-close-amended","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPopulations/Events/TypeIIPeriodCloseAmended.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The recorded close of a Type II period was amended by a named member under the exact admitted calendar intent, replacing the prior close. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPopulations/Events/TypeIIPeriodCloseAmended.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIIPeriodCloseApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodCloseApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodCloseApproved |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.type-i-i-period-close-approved","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPopulations/Events/TypeIIPeriodCloseApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The proposed close of a Type II period was approved by a named member, closing the observation period. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPopulations/Events/TypeIIPeriodCloseApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIIPeriodOpeningAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodOpeningAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodOpeningAccepted |
| Adopted wire | `{"discriminator":"bdgrz.type-ii-period.type-ii-period-opening-accepted","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodOpeningAccepted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.type-i-i-period.type-i-i-period-opening-accepted","version":1}` |
| Meaning | The root native acceptance of a period opening. The complete opening intent is persisted here before the calendar is touched; the period only exists once ObservationPeriodStarted follows. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodOpeningAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIIPeriodOpeningAccepted |
| Schema discriminator | bdgrz.type-i-i-period.type-i-i-period-opening-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
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
| [T053](../reactors/type-ii-range-reactor.md#t053) | physical root, logical prerequisite | TypeIIPeriod | AcquireTypeIIPeriodRange | TypeIIObservationCalendar |
| [T054](../reactors/type-ii-range-reactor.md#t054) | physical root | TypeIIObservationCalendar | ApplyTypeIIPeriodRange | TypeIIPeriod |
| [T055](../reactors/type-ii-range-reactor.md#t055) | physical root | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |
| [A018](../reactors/unassigned-architecture-rows.md#a018) | physical root, logical prerequisite | TypeIIPeriod | RejectTypeIIPeriodOpening | TypeIIPeriod |
| [A019](../reactors/unassigned-architecture-rows.md#a019) | physical root, logical prerequisite | TypeIIPeriod | RejectTypeIIPeriodAmendment | TypeIIPeriod |
| [A020](../reactors/unassigned-architecture-rows.md#a020) | physical root, logical prerequisite | TypeIIPeriod | CancelTypeIIPeriodAmendment | TypeIIPeriod |

### TypeIIPeriodOpeningRejected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIIPeriodOpeningRejected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPeriodOpeningRejected |
| Adopted wire | `{"discriminator":"bdgrz.type-ii-period.type-ii-period-opening-rejected","version":1}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodOpeningRejected.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.type-i-i-period.type-i-i-period-opening-rejected","version":1}` |
| Meaning | A pending opening was rejected because the calendar proved its range conflicts. The rejection fences a delayed apply; the same period identity is never reopened with changed intent. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/TypeIIPeriodOpeningRejected.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIIPeriodOpeningRejected |
| Schema discriminator | bdgrz.type-i-i-period.type-i-i-period-opening-rejected |
| Schema discriminatorVersion | 1 |
| Schema owner | TypeIIPeriod |
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
| [T055](../reactors/type-ii-range-reactor.md#t055) | physical root, logical prerequisite | TypeIIPeriod | SettleTypeIIPeriodRange | TypeIIObservationCalendar |
| [A018](../reactors/unassigned-architecture-rows.md#a018) | emitted semantic fact | TypeIIPeriod | RejectTypeIIPeriodOpening | TypeIIPeriod |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
