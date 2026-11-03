# ReadinessAssessment events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/readiness-assessment.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ReadinessAssessmentRecorded | ReadinessAssessmentRecorded | business | bdgrz.readiness.assessment.recorded | 1 | 18 | The immutable result of computing one readiness assessment from its frozen inputs, at the assessment owner's own revision one. Each manifest is the complete original collection; Intent is the frozen ReadinessRunIntent the computation used and nothing else. |

## Event details

### ReadinessAssessmentRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ReadinessAssessmentRecorded |
| Selected source event name | ReadinessAssessmentRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ReadinessAssessmentRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessAssessmentRecorded |
| Adopted wire | `{"discriminator":"bdgrz.readiness.assessment.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The immutable result of computing one readiness assessment from its frozen inputs, at the assessment owner's own revision one. Each manifest is the complete original collection; Intent is the frozen ReadinessRunIntent the computation used and nothing else. |
| Selected source meaning | The immutable result of computing one readiness assessment from its frozen inputs, at the assessment owner's own revision one. Each manifest is the complete original collection; Intent is the frozen ReadinessRunIntent the computation used and nothing else. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessAssessmentRecorded |
| Schema discriminator | bdgrz.readiness.assessment.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ReadinessAssessment |
| Schema existingContract | src/Compliance.Common/Features/Readiness/ReadinessAssessmentRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize complete IReadOnlyList<ReadinessInputView> to verified immutable content; preserve all items/order/identity, no truncation., Externalize complete IReadOnlyList<ReadinessFindingView> to verified immutable content; preserve all items/order/identity, no truncation., Externalize complete IReadOnlyList<ReadinessGapView> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Revision | long | true |
| AssessmentId | Uuid | true |
| RuleVersion | string | true |
| AsOf | DateTimeOffset | true |
| EditionId | Uuid? | true |
| InputFingerprint | string | true |
| InputsManifest | ImmutableContentRef | true |
| FindingsManifest | ImmutableContentRef | true |
| GapsManifest | ImmutableContentRef | true |
| RunnerMemberId | Uuid | true |
| RunBy | ActorReference | true |
| RunAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ReadinessAssessmentRecorded.InputsManifest.v1

| Property | Value |
| --- | --- |
| field | InputsManifest |
| schema | ReadinessAssessmentRecorded.InputsManifest.v1 |
| contentType | IReadOnlyList<ReadinessInputView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

#### Content schema: ReadinessAssessmentRecorded.FindingsManifest.v1

| Property | Value |
| --- | --- |
| field | FindingsManifest |
| schema | ReadinessAssessmentRecorded.FindingsManifest.v1 |
| contentType | IReadOnlyList<ReadinessFindingView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

#### Content schema: ReadinessAssessmentRecorded.GapsManifest.v1

| Property | Value |
| --- | --- |
| field | GapsManifest |
| schema | ReadinessAssessmentRecorded.GapsManifest.v1 |
| contentType | IReadOnlyList<ReadinessGapView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |

#### Content schema: readiness-run-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | readiness-run-intent.v1 |
| recordType | ReadinessRunIntent |
| rule | ProgramReadiness admits one pending latest adoption before computation. Freeze omitted AsOf once. Compute using only Inputs and matching RuleVersion. Successful computation is pending until exact program adoption; old latest cannot be decided while this admission is pending. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| ExpectedProgramRevision | ulong | true |
| AdoptionGeneration | ulong | true |
| RuleVersion | string | true |
| AsOf | DateTimeOffset | true |
| Inputs | ImmutableContentRef | true |
| RunnerMemberId | Uuid | true |

##### Variant: ReadinessRunIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| ExpectedProgramRevision | ulong | true |
| AdoptionGeneration | ulong | true |
| RuleVersion | string | true |
| AsOf | DateTimeOffset | true |
| Inputs | ImmutableContentRef | true |
| RunnerMemberId | Uuid | true |


#### Resolved intent schema

ReadinessRunIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T039](../reactors/reassess-program-readiness-reactor.md#t039) | logical prerequisite | ReadinessAssessment | AdoptReadinessAssessment | ProgramReadiness |
| [T087](../reactors/reassess-program-readiness-reactor.md#t087) | emitted semantic fact | ProgramReadiness | ComputeReadinessAssessment | ReadinessAssessment |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
