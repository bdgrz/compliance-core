# AccessPopulationException events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-population-exception.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AccessPopulationExceptionRecorded | business | bdgrz.access_population.exception_recorded | 2 | 7 | An expiring exception was approved for a system instance whose user population could not be supplied, recorded as the first revision of that exception. |

## Event details

### AccessPopulationExceptionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessPopulationExceptionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessPopulationExceptionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.access_population.exception_recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationExceptionRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_population.exception_recorded","version":1}` |
| Meaning | An expiring exception was approved for a system instance whose user population could not be supplied, recorded as the first revision of that exception. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessReviews/Events/AccessPopulationExceptionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessPopulationExceptionRecorded |
| Schema discriminator | bdgrz.access_population.exception_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessPopulationException |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessPopulationExceptionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessPopulationExceptionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SystemInstanceId | Uuid | true |
| Revision | long | true |
| ExceptionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessPopulationExceptionRecorded.ExceptionContent.v1

| Property | Value |
| --- | --- |
| field | ExceptionContent |
| schema | AccessPopulationExceptionRecorded.ExceptionContent.v1 |
| contentType | AccessPopulationExceptionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AccessPopulationExceptionRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
