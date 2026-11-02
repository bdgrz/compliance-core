# AccessExpectation events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/access-expectation.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AccessExpectationApproved | business | bdgrz.access_expectation.approved | 2 | 10 | A proposed expectation was approved. |
| AccessExpectationExceptionRecorded | business | bdgrz.access_expectation.exception_recorded | 2 | 7 | An expiring exception to an approved expectation was approved. The logical id of Operation is the command id, which is also the exception id; the complete AccessExpectationExceptionView is verified immutable content. |
| AccessExpectationProposed | business | bdgrz.access_expectation.proposed | 2 | 8 | An expectation version was proposed on its expectation line. The logical id of Operation is the expectation id; the complete AccessExpectationView is verified immutable content. |

## Event details

### AccessExpectationApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessExpectationApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessExpectationApproved |
| Adopted wire | `{"discriminator":"bdgrz.access_expectation.approved","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationApproved.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_expectation.approved","version":1}` |
| Meaning | A proposed expectation was approved. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessExpectationApproved |
| Schema discriminator | bdgrz.access_expectation.approved |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessExpectation |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessExpectationApproved.cs |
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
| SystemInstanceId | Uuid | true |
| Revision | long | true |
| ExpectationId | Uuid | true |
| ApprovedBy | ActorReference | true |
| ApprovedAt | DateTimeOffset | true |
| SeparationOfDutiesWaiverId | Uuid? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessExpectationExceptionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessExpectationExceptionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessExpectationExceptionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.access_expectation.exception_recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationExceptionRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_expectation.exception_recorded","version":1}` |
| Meaning | An expiring exception to an approved expectation was approved. The logical id of Operation is the command id, which is also the exception id; the complete AccessExpectationExceptionView is verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationExceptionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessExpectationExceptionRecorded |
| Schema discriminator | bdgrz.access_expectation.exception_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessExpectation |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessExpectationExceptionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessExpectationExceptionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
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

#### Content schema: AccessExpectationExceptionRecorded.ExceptionContent.v1

| Property | Value |
| --- | --- |
| field | ExceptionContent |
| schema | AccessExpectationExceptionRecorded.ExceptionContent.v1 |
| contentType | AccessExpectationExceptionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AccessExpectationProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessExpectationProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessExpectationProposed |
| Adopted wire | `{"discriminator":"bdgrz.access_expectation.proposed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationProposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.access_expectation.proposed","version":1}` |
| Meaning | An expectation version was proposed on its expectation line. The logical id of Operation is the expectation id; the complete AccessExpectationView is verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessReviews/Events/AccessExpectationProposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AccessExpectationProposed |
| Schema discriminator | bdgrz.access_expectation.proposed |
| Schema discriminatorVersion | 1 |
| Schema owner | AccessExpectation |
| Schema existingContract | src/Compliance.Common/Features/AccessReviews/AccessExpectationProposed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured AccessExpectationView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SystemInstanceId | Uuid | true |
| Revision | long | true |
| ProposerMemberId | Uuid | true |
| ExpectationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: AccessExpectationProposed.ExpectationContent.v1

| Property | Value |
| --- | --- |
| field | ExpectationContent |
| schema | AccessExpectationProposed.ExpectationContent.v1 |
| contentType | AccessExpectationView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **AccessExpectationApproved** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessExpectationExceptionRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **AccessExpectationProposed** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
