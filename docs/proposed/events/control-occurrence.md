# ControlOccurrence events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/control-occurrence.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ControlOccurrenceAttested | business | bdgrz.control.occurrence.attested | 2 | 9 | One attestation version of an occurrence. The complete ControlAttestationView is verified immutable content, so a large evidence list is never an event-size rejection. |
| ControlOccurrenceOpened | business | bdgrz.control.occurrence.opened | 2 | 21 | The one materialization of a control occurrence for its business window. CreationIntent is the complete frozen creation intent; Intent repeats the reference the opening operation accepted. A direct opening carries the first response; a materialization receiver fact does not. |
| ControlOccurrenceReviewed | business | bdgrz.control.occurrence.reviewed | 2 | 9 | The independent review decision on one exact attestation version. The complete ControlOccurrenceReviewView is verified immutable content. |

## Event details

### ControlOccurrenceAttested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlOccurrenceAttested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlOccurrenceAttested |
| Adopted wire | `{"discriminator":"bdgrz.control.occurrence.attested","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/ControlOccurrenceAttested.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.occurrence.attested","version":1}` |
| Meaning | One attestation version of an occurrence. The complete ControlAttestationView is verified immutable content, so a large evidence list is never an event-size rejection. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/ControlOccurrenceAttested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlOccurrenceAttested |
| Schema discriminator | bdgrz.control.occurrence.attested |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlOccurrence |
| Schema existingContract | src/Compliance.Common/Features/Operations/ControlOccurrenceAttested.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ControlAttestationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| Revision | long | true |
| AttestationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlOccurrenceAttested.AttestationContent.v1

| Property | Value |
| --- | --- |
| field | AttestationContent |
| schema | ControlOccurrenceAttested.AttestationContent.v1 |
| contentType | ControlAttestationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S002](../reactors/control-occurrence-finding-reactor.md#s002) | physical root, logical prerequisite | ControlOccurrence | RaiseOccurrenceFinding | FindingAggregate |

### ControlOccurrenceOpened

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlOccurrenceOpened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlOccurrenceOpened |
| Adopted wire | `{"discriminator":"bdgrz.control.occurrence.opened","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/ControlOccurrenceOpened.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.occurrence.opened","version":1}` |
| Meaning | The one materialization of a control occurrence for its business window. CreationIntent is the complete frozen creation intent; Intent repeats the reference the opening operation accepted. A direct opening carries the first response; a materialization receiver fact does not. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/ControlOccurrenceOpened.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlOccurrenceOpened |
| Schema discriminator | bdgrz.control.occurrence.opened |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlOccurrence |
| Schema existingContract | src/Compliance.Common/Features/Operations/ControlOccurrenceOpened.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| Revision | long | true |
| Kind | string | true |
| ControlVersionId | Uuid | true |
| PlanVersionId | Uuid | true |
| PeriodStart | DateOnly? | true |
| PeriodEnd | DateOnly? | true |
| DueOn | DateOnly? | true |
| Trigger | string? | true |
| Assignee | OperatingHolder | true |
| OpenedBy | ActorReference | true |
| OpenedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| BusinessWindow | ControlBusinessWindow | true |
| CreationIntent | ImmutableContentRef | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: control-occurrence-creation-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | control-occurrence-creation-intent.v1 |
| recordType | ControlOccurrenceCreationIntent |
| rule | OccurrenceId derives solely from tenant/program/control/canonical window using the selected common namespace. Trigger/generator/period/plan version does not create another identity. All generators freeze these exact values; two differing full intents for the same canonical window conflict. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| Window | ControlBusinessWindow | true |
| PlanVersionId | Uuid | true |
| PlanProof | SourceFactLocator | true |
| Owner | OperatingHolder | true |
| BackupOwner | OperatingHolder? | true |
| ReviewerMemberId | Uuid | true |
| Trigger | string | true |
| SelectedAt | DateTimeOffset | true |

##### Variant: ControlOccurrenceCreationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| Window | ControlBusinessWindow | true |
| PlanVersionId | Uuid | true |
| PlanProof | SourceFactLocator | true |
| Owner | OperatingHolder | true |
| BackupOwner | OperatingHolder? | true |
| ReviewerMemberId | Uuid | true |
| Trigger | string | true |
| SelectedAt | DateTimeOffset | true |


#### Resolved intent schema

ControlOccurrenceCreationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T040](../reactors/control-occurrence-fan-out-reactor.md#t040) | emitted semantic fact | TypeIIPeriod | MaterializeControlOccurrence | ControlOccurrence |

### ControlOccurrenceReviewed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlOccurrenceReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlOccurrenceReviewed |
| Adopted wire | `{"discriminator":"bdgrz.control.occurrence.reviewed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/ControlOccurrenceReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.occurrence.reviewed","version":1}` |
| Meaning | The independent review decision on one exact attestation version. The complete ControlOccurrenceReviewView is verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/ControlOccurrenceReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlOccurrenceReviewed |
| Schema discriminator | bdgrz.control.occurrence.reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlOccurrence |
| Schema existingContract | src/Compliance.Common/Features/Operations/ControlOccurrenceReviewed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ControlOccurrenceReviewView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| OccurrenceId | Uuid | true |
| Revision | long | true |
| ReviewContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlOccurrenceReviewed.ReviewContent.v1

| Property | Value |
| --- | --- |
| field | ReviewContent |
| schema | ControlOccurrenceReviewed.ReviewContent.v1 |
| contentType | ControlOccurrenceReviewView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S002](../reactors/control-occurrence-finding-reactor.md#s002) | physical root, logical prerequisite | ControlOccurrence | RaiseOccurrenceFinding | FindingAggregate |


## Wire/schema version discrepancies

- **ControlOccurrenceAttested** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlOccurrenceOpened** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlOccurrenceReviewed** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
