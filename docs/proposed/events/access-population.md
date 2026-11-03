# AccessPopulation events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-population.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AccessPopulationAccepted | AccessPopulationAccepted | business | bdgrz.access_population.accepted | 2 | 12 | A population was attested and frozen as an immutable snapshot of its raw facts. |
| AccessPopulationFactsRecorded | AccessPopulationFactsRecorded | business | bdgrz.access_population.facts_recorded | 2 | 9 | A draft's complete observed facts were replaced. FactsContent is the complete AccessPopulationFacts as verified immutable content, so no list can be truncated by an event size. |
| AccessPopulationOpened | AccessPopulationOpened | business | bdgrz.access_population.opened | 2 | 13 | A population draft was opened for one exact system-instance revision. It is the root native fact of the population's opening operation; the logical id of Operation is the population id. |

## Event details

### AccessPopulationAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessPopulationAccepted |
| Selected source event name | AccessPopulationAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessPopulationAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessPopulationAccepted |
| Adopted wire | `{"discriminator":"bdgrz.access_population.accepted","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationAccepted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_population.accepted","version":1}` |
| Target meaning | A population was attested and frozen as an immutable snapshot of its raw facts. |
| Selected source meaning | A population was attested and frozen as an immutable snapshot of its raw facts. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessPopulationAccepted |
| Schema discriminator | bdgrz.access_population.accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessPopulation |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessPopulationAccepted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| Revision | long | true |
| SnapshotId | Uuid | true |
| ContentSha256 | string | true |
| CalculationId | Uuid | true |
| Attestation | string | true |
| AcceptedBy | ActorReference | true |
| AcceptedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessPopulationFactsRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessPopulationFactsRecorded |
| Selected source event name | AccessPopulationFactsRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessPopulationFactsRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessPopulationFactsRecorded |
| Adopted wire | `{"discriminator":"bdgrz.access_population.facts_recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationFactsRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_population.facts_recorded","version":1}` |
| Target meaning | A draft's complete observed facts were replaced. FactsContent is the complete AccessPopulationFacts as verified immutable content, so no list can be truncated by an event size. |
| Selected source meaning | A draft's complete observed facts were replaced. FactsContent is the complete AccessPopulationFacts as verified immutable content, so no list can be truncated by an event size. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationFactsRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessPopulationFactsRecorded |
| Schema discriminator | bdgrz.access_population.facts_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessPopulation |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessPopulationFactsRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessPopulationFacts including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| Revision | long | true |
| FactsContent | ImmutableContentRef | true |
| RecordedBy | ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessPopulationFactsRecorded.FactsContent.v1

| Property | Value |
| --- | --- |
| field | FactsContent |
| schema | AccessPopulationFactsRecorded.FactsContent.v1 |
| contentType | AccessPopulationFacts |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessPopulationOpened

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessPopulationOpened |
| Selected source event name | AccessPopulationOpened |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessPopulationOpened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessPopulationOpened |
| Adopted wire | `{"discriminator":"bdgrz.access_population.opened","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationOpened.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_population.opened","version":1}` |
| Target meaning | A population draft was opened for one exact system-instance revision. It is the root native fact of the population's opening operation; the logical id of Operation is the population id. |
| Selected source meaning | A population draft was opened for one exact system-instance revision. It is the root native fact of the population's opening operation; the logical id of Operation is the population id. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationOpened.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessPopulationOpened |
| Schema discriminator | bdgrz.access_population.opened |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessPopulation |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessPopulationOpened.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| PopulationId | Uuid | true |
| ApplicationId | Uuid | true |
| SystemInstanceId | Uuid | true |
| SystemInstanceRevision | long | true |
| ObservedAt | DateTimeOffset | true |
| SourceKind | string | true |
| Source | string | true |
| OpenedBy | ActorReference | true |
| OpenedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AccessPopulationAccepted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessPopulationFactsRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessPopulationOpened** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
