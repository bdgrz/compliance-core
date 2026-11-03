# ControlEvaluation events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/control-evaluation.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ControlEvaluationDeviationDisposed | ControlEvaluationDeviationDisposed | business | bdgrz.control.evaluation.deviation_disposed | 2 | 12 | The disposition of one minor deviation of one evaluation. |
| ControlEvaluationReviewed | ControlEvaluationReviewed | business | bdgrz.control.evaluation.reviewed | 2 | 9 | The independent review decision on the submitted round. Decision (accepted, rejected or changes_requested) decides the lifecycle of the evaluation; the complete decision is verified immutable content. |
| ControlEvaluationStarted | ControlEvaluationStarted | business | bdgrz.control.evaluation.started | 2 | 15 | The start of one evaluation, which is its own owner stream. The frozen procedure and the retested deviation ids are verified immutable content, so nothing is truncated. A retest names the accepted review fact of its predecessor in RetestPredecessor; the predecessor itself is never written. |
| ControlEvaluationStepRecorded | ControlEvaluationStepRecorded | business | bdgrz.control.evaluation.step_recorded | 2 | 11 | A step result of one evaluation. The result with its complete inspected items and the deviation it raised, if any, are verified immutable content. |
| ControlEvaluationSubmitted | ControlEvaluationSubmitted | business | bdgrz.control.evaluation.submitted | 2 | 9 | One submitted round of one evaluation. The complete submission, with the exact step results and deviations it rests on, is verified immutable content. |

## Event details

### ControlEvaluationDeviationDisposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlEvaluationDeviationDisposed |
| Selected source event name | ControlEvaluationDeviationDisposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlEvaluationDeviationDisposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlEvaluationDeviationDisposed |
| Adopted wire | `{"discriminator":"bdgrz.control.evaluation.deviation_disposed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationDeviationDisposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.evaluation.deviation_disposed","version":1}` |
| Target meaning | The disposition of one minor deviation of one evaluation. |
| Selected source meaning | The disposition of one minor deviation of one evaluation. |
| Meaning source | record summary in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationDeviationDisposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlEvaluationDeviationDisposed |
| Schema discriminator | bdgrz.control.evaluation.deviation_disposed |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlEvaluation |
| Schema existingContract | src/Compliance.Common/Features/Evaluations/ControlEvaluationDeviationDisposed.cs |
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
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| Revision | long | true |
| DeviationId | Uuid | true |
| Disposition | string | true |
| Rationale | string | true |
| WaiverId | Uuid? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlEvaluationReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlEvaluationReviewed |
| Selected source event name | ControlEvaluationReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlEvaluationReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlEvaluationReviewed |
| Adopted wire | `{"discriminator":"bdgrz.control.evaluation.reviewed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.evaluation.reviewed","version":1}` |
| Target meaning | The independent review decision on the submitted round. Decision (accepted, rejected or changes_requested) decides the lifecycle of the evaluation; the complete decision is verified immutable content. |
| Selected source meaning | The independent review decision on the submitted round. Decision (accepted, rejected or changes_requested) decides the lifecycle of the evaluation; the complete decision is verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlEvaluationReviewed |
| Schema discriminator | bdgrz.control.evaluation.reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlEvaluation |
| Schema existingContract | src/Compliance.Common/Features/Evaluations/ControlEvaluationReviewed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ControlEvaluationReviewView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| Revision | long | true |
| ReviewContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlEvaluationReviewed.ReviewContent.v1

| Property | Value |
| --- | --- |
| field | ReviewContent |
| schema | ControlEvaluationReviewed.ReviewContent.v1 |
| contentType | ControlEvaluationReviewView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlEvaluationStarted

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlEvaluationStarted |
| Selected source event name | ControlEvaluationStarted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlEvaluationStarted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlEvaluationStarted |
| Adopted wire | `{"discriminator":"bdgrz.control.evaluation.started","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationStarted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.evaluation.started","version":1}` |
| Target meaning | The start of one evaluation, which is its own owner stream. The frozen procedure and the retested deviation ids are verified immutable content, so nothing is truncated. A retest names the accepted review fact of its predecessor in RetestPredecessor; the predecessor itself is never written. |
| Selected source meaning | The start of one evaluation, which is its own owner stream. The frozen procedure and the retested deviation ids are verified immutable content, so nothing is truncated. A retest names the accepted review fact of its predecessor in RetestPredecessor; the predecessor itself is never written. |
| Meaning source | record summary in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationStarted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlEvaluationStarted |
| Schema discriminator | bdgrz.control.evaluation.started |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlEvaluation |
| Schema existingContract | src/Compliance.Common/Features/Evaluations/ControlEvaluationStarted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<EvaluationProcedureStep> to verified immutable content; preserve all items/order/identity, no truncation., Externalize complete IReadOnlyList<Uuid> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| Revision | long | true |
| ControlVersionId | Uuid | true |
| StepsManifest | ImmutableContentRef | true |
| EvaluatorMemberId | Uuid | true |
| StartedBy | ActorReference | true |
| StartedAt | DateTimeOffset | true |
| RetestOfEvaluationId | Uuid? | true |
| RetestOfDeviationIdsManifest | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlEvaluationStarted.StepsManifest.v1

| Property | Value |
| --- | --- |
| field | StepsManifest |
| schema | ControlEvaluationStarted.StepsManifest.v1 |
| contentType | IReadOnlyList<EvaluationProcedureStep> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

#### Content schema: ControlEvaluationStarted.RetestOfDeviationIdsManifest.v1

| Property | Value |
| --- | --- |
| field | RetestOfDeviationIdsManifest |
| schema | ControlEvaluationStarted.RetestOfDeviationIdsManifest.v1 |
| contentType | IReadOnlyList<Uuid> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlEvaluationStepRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlEvaluationStepRecorded |
| Selected source event name | ControlEvaluationStepRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlEvaluationStepRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlEvaluationStepRecorded |
| Adopted wire | `{"discriminator":"bdgrz.control.evaluation.step_recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationStepRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.evaluation.step_recorded","version":1}` |
| Target meaning | A step result of one evaluation. The result with its complete inspected items and the deviation it raised, if any, are verified immutable content. |
| Selected source meaning | A step result of one evaluation. The result with its complete inspected items and the deviation it raised, if any, are verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationStepRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlEvaluationStepRecorded |
| Schema discriminator | bdgrz.control.evaluation.step_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlEvaluation |
| Schema existingContract | src/Compliance.Common/Features/Evaluations/ControlEvaluationStepRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured EvaluationStepResultView including nested collections; a former item cap cannot become an event-size rejection for valid larger content., Externalize structured EvaluationDeviationView? including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| Revision | long | true |
| StepId | Uuid | true |
| ResultContent | ImmutableContentRef | true |
| DeviationContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlEvaluationStepRecorded.ResultContent.v1

| Property | Value |
| --- | --- |
| field | ResultContent |
| schema | ControlEvaluationStepRecorded.ResultContent.v1 |
| contentType | EvaluationStepResultView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: ControlEvaluationStepRecorded.DeviationContent.v1

| Property | Value |
| --- | --- |
| field | DeviationContent |
| schema | ControlEvaluationStepRecorded.DeviationContent.v1 |
| contentType | EvaluationDeviationView? |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlEvaluationSubmitted

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlEvaluationSubmitted |
| Selected source event name | ControlEvaluationSubmitted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlEvaluationSubmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlEvaluationSubmitted |
| Adopted wire | `{"discriminator":"bdgrz.control.evaluation.submitted","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationSubmitted.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.evaluation.submitted","version":1}` |
| Target meaning | One submitted round of one evaluation. The complete submission, with the exact step results and deviations it rests on, is verified immutable content. |
| Selected source meaning | One submitted round of one evaluation. The complete submission, with the exact step results and deviations it rests on, is verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/Evaluations/Events/ControlEvaluationSubmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlEvaluationSubmitted |
| Schema discriminator | bdgrz.control.evaluation.submitted |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlEvaluation |
| Schema existingContract | src/Compliance.Common/Features/Evaluations/ControlEvaluationSubmitted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured EvaluationSubmissionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| EvaluationId | Uuid | true |
| Revision | long | true |
| SubmissionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlEvaluationSubmitted.SubmissionContent.v1

| Property | Value |
| --- | --- |
| field | SubmissionContent |
| schema | ControlEvaluationSubmitted.SubmissionContent.v1 |
| contentType | EvaluationSubmissionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S001](../reactors/control-evaluation-deviation-finding-reactor.md#s001) | physical root, logical prerequisite | ControlEvaluation | RaiseEvaluationDeviationFinding | FindingAggregate |


## Wire/schema version discrepancies

- **ControlEvaluationDeviationDisposed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlEvaluationReviewed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlEvaluationStarted** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlEvaluationStepRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlEvaluationSubmitted** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
