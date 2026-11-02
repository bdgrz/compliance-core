# ProgramReadiness events

Canonical events: **7**. Complete canonical event and available schema records: [owner event JSON](../data/events/program-readiness.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ReadinessAssessmentAdopted | business | bdgrz.program-readiness.readiness-assessment-adopted | 1 | 8 | The exact computed assessment became the latest of the program. Generation is the pending adoption generation the run reserved; Operation.RootOperationId is the assessment identity. SourceProofs holds the request and recorded-assessment proofs, and OperationResult the original registration with the program revision after this adoption. |
| ReadinessAssessmentRequested | business | bdgrz.program-readiness.readiness-assessment-requested | 1 | 8 | The root native acceptance of a readiness run. It reserves the one pending latest-adoption generation Generation and references the frozen ReadinessRunIntent with its complete input snapshot; the assessment is only the latest once ReadinessAssessmentAdopted follows. |
| ReadinessDecisionAccepted | business | bdgrz.program-readiness.readiness-decision-accepted | 1 | 8 | A management readiness decision for a program was accepted, sealing the latest adoption generation and the gap plans it relies on. |
| ReadinessDecisionCanceled | business | bdgrz.program-readiness.readiness-decision-canceled | 1 | 8 | A pending decision ended before it was recorded. The fact is the local receiver fence: a delayed completion sees it and does nothing, and only then are the gap plan uses settled. SourceProofs is the failure proof. |
| ReadinessDecisionRecorded | business | bdgrz.readiness.decision.recorded | 1 | 11 | The management readiness decision was recorded once every gap plan was held. DecisionContent is the complete ReadinessDecisionView, DecisionProofs the complete manifest of the held gap plan proofs, and OperationResult the original typed result of the decision command. |
| TypeIEntryDecisionAccepted | business | bdgrz.program-readiness.type-i-entry-decision-accepted | 1 | 8 | A decision to enter a Type I examination was accepted for a program, sealing the latest adoption generation and the gap plans it relies on. |
| TypeIEntryDecisionRecorded | business | bdgrz.readiness.type_i_entry.decided | 1 | 11 | The Type I entry decision was recorded once every gap plan was held. DecisionContent is the complete TypeIEntryDecisionView with its frozen unresolved items, DecisionProofs the complete manifest of the held gap plan proofs, and OperationResult the original typed result of the command. |

## Event details

### ReadinessAssessmentAdopted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ReadinessAssessmentAdopted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessAssessmentAdopted |
| Adopted wire | `{"discriminator":"bdgrz.program-readiness.readiness-assessment-adopted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentAdopted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The exact computed assessment became the latest of the program. Generation is the pending adoption generation the run reserved; Operation.RootOperationId is the assessment identity. SourceProofs holds the request and recorded-assessment proofs, and OperationResult the original registration with the program revision after this adoption. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentAdopted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessAssessmentAdopted |
| Schema discriminator | bdgrz.program-readiness.readiness-assessment-adopted |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
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
| [T039](../reactors/readiness-run-reactor.md#t039) | emitted semantic fact | ReadinessAssessment | AdoptReadinessAssessment | ProgramReadiness |

### ReadinessAssessmentRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ReadinessAssessmentRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessAssessmentRequested |
| Adopted wire | `{"discriminator":"bdgrz.program-readiness.readiness-assessment-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentRequested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of a readiness run. It reserves the one pending latest-adoption generation Generation and references the frozen ReadinessRunIntent with its complete input snapshot; the assessment is only the latest once ReadinessAssessmentAdopted follows. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessAssessmentRequested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessAssessmentRequested |
| Schema discriminator | bdgrz.program-readiness.readiness-assessment-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
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
| [T039](../reactors/readiness-run-reactor.md#t039) | physical root | ReadinessAssessment | AdoptReadinessAssessment | ProgramReadiness |
| [T087](../reactors/readiness-run-reactor.md#t087) | physical root, logical prerequisite | ProgramReadiness | ComputeReadinessAssessment | ReadinessAssessment |

### ReadinessDecisionAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ReadinessDecisionAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessDecisionAccepted |
| Adopted wire | `{"discriminator":"bdgrz.program-readiness.readiness-decision-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A management readiness decision for a program was accepted, sealing the latest adoption generation and the gap plans it relies on. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessDecisionAccepted |
| Schema discriminator | bdgrz.program-readiness.readiness-decision-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
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

#### Content schema: readiness-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | readiness-decision-intent.v1 |
| recordType | ReadinessDecisionIntent |
| rule | Seal latest adoption and gap-edit admission. Drain older admitted gap edits before grants; if any frozen expected plan changed, fence/abort this decision rather than pick newer input. Require all current rules/plan/independence checks at the decision instant. One approved entry slot per program. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ReadinessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ReadinessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T065](../reactors/readiness-decision-reactor.md#t065) | physical root, logical prerequisite | ProgramReadiness | AuthorizeReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [T066](../reactors/readiness-decision-reactor.md#t066) | physical root | ProgramReadiness | SettleReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [T074](../reactors/readiness-decision-reactor.md#t074) | physical root, logical prerequisite | ProgramReadiness | CompleteReadinessDecision | ProgramReadiness |
| [T075](../reactors/readiness-decision-reactor.md#t075) | physical root | ProgramReadiness | CompleteTypeIEntryDecision | ProgramReadiness |
| [A009](../reactors/unassigned-architecture-rows.md#a009) | physical root, logical prerequisite | ProgramReadiness | AbortReadinessDecision | ProgramReadiness |

### ReadinessDecisionCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ReadinessDecisionCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessDecisionCanceled |
| Adopted wire | `{"discriminator":"bdgrz.program-readiness.readiness-decision-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A pending decision ended before it was recorded. The fact is the local receiver fence: a delayed completion sees it and does nothing, and only then are the gap plan uses settled. SourceProofs is the failure proof. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessDecisionCanceled |
| Schema discriminator | bdgrz.program-readiness.readiness-decision-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
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

#### Content schema: readiness-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | readiness-decision-intent.v1 |
| recordType | ReadinessDecisionIntent |
| rule | Seal latest adoption and gap-edit admission. Drain older admitted gap edits before grants; if any frozen expected plan changed, fence/abort this decision rather than pick newer input. Require all current rules/plan/independence checks at the decision instant. One approved entry slot per program. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ReadinessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ReadinessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T066](../reactors/readiness-decision-reactor.md#t066) | logical prerequisite | ProgramReadiness | SettleReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [A009](../reactors/unassigned-architecture-rows.md#a009) | emitted semantic fact | ProgramReadiness | AbortReadinessDecision | ProgramReadiness |

### ReadinessDecisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ReadinessDecisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ReadinessDecisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.readiness.decision.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The management readiness decision was recorded once every gap plan was held. DecisionContent is the complete ReadinessDecisionView, DecisionProofs the complete manifest of the held gap plan proofs, and OperationResult the original typed result of the decision command. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/ReadinessDecisionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ReadinessDecisionRecorded |
| Schema discriminator | bdgrz.readiness.decision.recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
| Schema existingContract | src/Compliance.Common/Features/Readiness/ReadinessDecisionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured ReadinessDecisionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Revision | long | true |
| DecisionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| AssessmentId | Uuid | true |
| DecisionId | Uuid | true |
| DecisionProofs | ImmutableContentRef | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ReadinessDecisionRecorded.DecisionContent.v1

| Property | Value |
| --- | --- |
| field | DecisionContent |
| schema | ReadinessDecisionRecorded.DecisionContent.v1 |
| contentType | ReadinessDecisionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: readiness-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | readiness-decision-intent.v1 |
| recordType | ReadinessDecisionIntent |
| rule | Seal latest adoption and gap-edit admission. Drain older admitted gap edits before grants; if any frozen expected plan changed, fence/abort this decision rather than pick newer input. Require all current rules/plan/independence checks at the decision instant. One approved entry slot per program. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ReadinessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ReadinessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T066](../reactors/readiness-decision-reactor.md#t066) | logical prerequisite | ProgramReadiness | SettleReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [T074](../reactors/readiness-decision-reactor.md#t074) | emitted semantic fact | ProgramReadiness | CompleteReadinessDecision | ProgramReadiness |

### TypeIEntryDecisionAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIEntryDecisionAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIEntryDecisionAccepted |
| Adopted wire | `{"discriminator":"bdgrz.program-readiness.type-i-entry-decision-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/TypeIEntryDecisionAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A decision to enter a Type I examination was accepted for a program, sealing the latest adoption generation and the gap plans it relies on. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Readiness/Events/TypeIEntryDecisionAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIEntryDecisionAccepted |
| Schema discriminator | bdgrz.program-readiness.type-i-entry-decision-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
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

#### Content schema: readiness-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | readiness-decision-intent.v1 |
| recordType | ReadinessDecisionIntent |
| rule | Seal latest adoption and gap-edit admission. Drain older admitted gap edits before grants; if any frozen expected plan changed, fence/abort this decision rather than pick newer input. Require all current rules/plan/independence checks at the decision instant. One approved entry slot per program. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ReadinessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ReadinessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T065](../reactors/readiness-decision-reactor.md#t065) | physical root, logical prerequisite | ProgramReadiness | AuthorizeReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [T066](../reactors/readiness-decision-reactor.md#t066) | physical root | ProgramReadiness | SettleReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [T074](../reactors/readiness-decision-reactor.md#t074) | physical root | ProgramReadiness | CompleteReadinessDecision | ProgramReadiness |
| [T075](../reactors/readiness-decision-reactor.md#t075) | physical root, logical prerequisite | ProgramReadiness | CompleteTypeIEntryDecision | ProgramReadiness |
| [A009](../reactors/unassigned-architecture-rows.md#a009) | physical root, logical prerequisite | ProgramReadiness | AbortReadinessDecision | ProgramReadiness |

### TypeIEntryDecisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TypeIEntryDecisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIEntryDecisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.readiness.type_i_entry.decided","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Readiness/Events/TypeIEntryDecisionRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The Type I entry decision was recorded once every gap plan was held. DecisionContent is the complete TypeIEntryDecisionView with its frozen unresolved items, DecisionProofs the complete manifest of the held gap plan proofs, and OperationResult the original typed result of the command. |
| Meaning source | record summary in src/Compliance.Common/Features/Readiness/Events/TypeIEntryDecisionRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TypeIEntryDecisionRecorded |
| Schema discriminator | bdgrz.readiness.type_i_entry.decided |
| Schema discriminatorVersion | 1 |
| Schema owner | ProgramReadiness |
| Schema existingContract | src/Compliance.Common/Features/Readiness/TypeIEntryDecisionRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured TypeIEntryDecisionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| Revision | long | true |
| DecisionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| AssessmentId | Uuid | true |
| DecisionId | Uuid | true |
| DecisionProofs | ImmutableContentRef | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: TypeIEntryDecisionRecorded.DecisionContent.v1

| Property | Value |
| --- | --- |
| field | DecisionContent |
| schema | TypeIEntryDecisionRecorded.DecisionContent.v1 |
| contentType | TypeIEntryDecisionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: readiness-decision-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | readiness-decision-intent.v1 |
| recordType | ReadinessDecisionIntent |
| rule | Seal latest adoption and gap-edit admission. Drain older admitted gap edits before grants; if any frozen expected plan changed, fence/abort this decision rather than pick newer input. Require all current rules/plan/independence checks at the decision instant. One approved entry slot per program. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ReadinessDecisionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ProgramId | Uuid | true |
| AssessmentId | Uuid | true |
| AssessmentProof | SourceFactLocator | true |
| DecisionKind | ReadinessDecisionKind | true |
| Outcome | string | true |
| Rationale | string | true |
| RuleVersion | string | true |
| GapSelection | ImmutableSelectionManifest | true |
| AcknowledgedGapIds | ImmutableSelectionManifest | true |
| RequiredPlanUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ReadinessDecisionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T066](../reactors/readiness-decision-reactor.md#t066) | logical prerequisite | ProgramReadiness | SettleReadinessGapPlanReadinessDecisionUse | ReadinessGapPlan |
| [T075](../reactors/readiness-decision-reactor.md#t075) | emitted semantic fact | ProgramReadiness | CompleteTypeIEntryDecision | ProgramReadiness |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
