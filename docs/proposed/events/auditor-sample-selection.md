# AuditorSampleSelection events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/auditor-sample-selection.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| SampleSelectionRecorded | business | bdgrz.auditorrequests.sample-selection-recorded | 1 | 9 | The materialization of an admitted sample selection on its own owner, at revision one. Change repeats the frozen AuditorSampleSelectionIntent, so the selection never rereads the request or the latest population. |

## Event details

### SampleSelectionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:SampleSelectionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SampleSelectionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.auditorrequests.sample-selection-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditorRequests/Events/SampleSelectionRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The materialization of an admitted sample selection on its own owner, at revision one. Change repeats the frozen AuditorSampleSelectionIntent, so the selection never rereads the request or the latest population. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditorRequests/Events/SampleSelectionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SampleSelectionRecorded |
| Schema discriminator | bdgrz.auditorrequests.sample-selection-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditorSampleSelection |
| Schema existingContract | src/Compliance.Common/Features/AuditorRequests/SampleSelectionRecorded.cs |
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
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: auditor-sample-selection-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | auditor-sample-selection-intent.v1 |
| recordType | AuditorSampleSelectionIntent |
| rule | Open request admits exact nonempty duplicate-free row selection and frozen population content. Amendment identifies a prior admitted selection of the same population with strictly later population version. Freeze request revision separately from new selection zero. Resolution drains every admitted selection; later closure cannot discard it. Child materializes the exact admitted selection without a live parent read. |
| sourceCommandVariants | MaterializeAuditorSampleSelection |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RequestId | Uuid | true |
| RequestGeneration | ulong | true |
| ParentAdmissionRevision | ulong | true |
| SelectionId | Uuid | true |
| SupersedesSelectionId | Uuid? | true |
| PopulationId | Uuid | true |
| PopulationVersion | int | true |
| PopulationContent | ImmutableContentRef | true |
| PopulationRowIds | ImmutableSelectionManifest | true |
| SelectionMethod | string | true |
| Source | string | true |
| ExternalReference | string | true |
| AmendmentReason | string? | true |
| RecordedAt | DateTimeOffset | true |

##### Variant: AuditorSampleSelectionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| RequestId | Uuid | true |
| RequestGeneration | ulong | true |
| ParentAdmissionRevision | ulong | true |
| SelectionId | Uuid | true |
| SupersedesSelectionId | Uuid? | true |
| PopulationId | Uuid | true |
| PopulationVersion | int | true |
| PopulationContent | ImmutableContentRef | true |
| PopulationRowIds | ImmutableSelectionManifest | true |
| SelectionMethod | string | true |
| Source | string | true |
| ExternalReference | string | true |
| AmendmentReason | string? | true |
| RecordedAt | DateTimeOffset | true |


#### Resolved intent schema

AuditorSampleSelectionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T105](../reactors/auditor-sample-selection-materialization-reactor.md#t105) | emitted semantic fact | AuditorRequest | MaterializeAuditorSampleSelection | AuditorSampleSelection |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
