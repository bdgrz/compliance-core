# SystemDescription events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/system-description.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| SystemDescriptionApproved | SystemDescriptionApproved | business | bdgrz.systemdescription.system-description-approved | 2 | 9 | A system description version was approved by a named member, making it the approved description for the program. |
| SystemDescriptionLinkedToExamination | SystemDescriptionBound | business | bdgrz.systemdescription.system-description-bound | 2 | 9 | The approved system description version was bound to the examination snapshot it describes, fixing which examination the description supports. |
| SystemDescriptionDraftCreated | SystemDescriptionDraftCreated | business | bdgrz.systemdescription.system-description-draft-created | 2 | 9 | A system description draft was created for a program with its first authored version by a named member, awaiting review. |
| SystemDescriptionDraftRevised | SystemDescriptionDraftRevised | business | bdgrz.systemdescription.system-description-draft-revised | 2 | 9 | A system description draft was revised into a new version by a named member, awaiting review. |
| SystemDescriptionNoChangeRecorded | SystemDescriptionNoChangeRecorded | business | bdgrz.systemdescription.system-description-no-change-recorded | 2 | 9 | A non-material change in the period was recorded with a reasoned decision by a named member that the system description needs no update. |
| SystemDescriptionReviewed | SystemDescriptionReviewed | business | bdgrz.systemdescription.system-description-reviewed | 2 | 9 | A system description version was reviewed with a decision by a named reviewer. |
| SystemDescriptionSourcesRecorded | SystemDescriptionSourcesBound | business | bdgrz.systemdescription.system-description-sources-bound | 2 | 9 | A new system description draft version was reconciled against its current sources by a named member, recording the sources the description now relies on. |
| SystemDescriptionSuccessorProposed | SystemDescriptionSuccessorProposed | business | bdgrz.systemdescription.system-description-successor-proposed | 2 | 9 | A successor draft of an approved system description was proposed with a new version by a named member, awaiting review. |

## Event details

### SystemDescriptionApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionApproved |
| Selected source event name | SystemDescriptionApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemDescriptionApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionApproved |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-approved","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionApproved.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-approved","version":1}` |
| Target meaning | A system description version was approved by a named member, making it the approved description for the program. |
| Selected source meaning | A system description version was approved by a named member, making it the approved description for the program. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionApproved |
| Schema discriminator | bdgrz.systemdescription.system-description-approved |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionApproved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionApprovalView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| ApprovalContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionApproved.ApprovalContent.v1

| Property | Value |
| --- | --- |
| field | ApprovalContent |
| schema | SystemDescriptionApproved.ApprovalContent.v1 |
| contentType | SystemDescriptionApprovalView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S020](../reactors/system-description-decision-reactor.md#s020) | physical root, logical prerequisite | SystemDescription | RecordSignificantChangeSystemDescriptionDecision | SignificantChange |
| [S021](../reactors/system-description-decision-reactor.md#s021) | physical root | SystemDescription | RecordComplianceIncidentSystemDescriptionDecision | ComplianceIncident |

### SystemDescriptionLinkedToExamination

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionLinkedToExamination |
| Selected source event name | SystemDescriptionBound |
| Name decision | Names which approved system description version supports the exact examination snapshot. |
| Canonical semantic key | event:SystemDescriptionBound |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionBound |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-bound","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionBound.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-bound","version":1}` |
| Target meaning | The approved system description version was bound to the examination snapshot it describes, fixing which examination the description supports. |
| Selected source meaning | The approved system description version was bound to the examination snapshot it describes, fixing which examination the description supports. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionBound.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionBound |
| Schema discriminator | bdgrz.systemdescription.system-description-bound |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionBound.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionBindingView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| BindingContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionBound.BindingContent.v1

| Property | Value |
| --- | --- |
| field | BindingContent |
| schema | SystemDescriptionBound.BindingContent.v1 |
| contentType | SystemDescriptionBindingView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemDescriptionDraftCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionDraftCreated |
| Selected source event name | SystemDescriptionDraftCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemDescriptionDraftCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionDraftCreated |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-draft-created","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionDraftCreated.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-draft-created","version":1}` |
| Target meaning | A system description draft was created for a program with its first authored version by a named member, awaiting review. |
| Selected source meaning | A system description draft was created for a program with its first authored version by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionDraftCreated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionDraftCreated |
| Schema discriminator | bdgrz.systemdescription.system-description-draft-created |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionDraftCreated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionVersionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| VersionContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionDraftCreated.VersionContent.v1

| Property | Value |
| --- | --- |
| field | VersionContent |
| schema | SystemDescriptionDraftCreated.VersionContent.v1 |
| contentType | SystemDescriptionVersionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemDescriptionDraftRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionDraftRevised |
| Selected source event name | SystemDescriptionDraftRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemDescriptionDraftRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionDraftRevised |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-draft-revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionDraftRevised.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-draft-revised","version":1}` |
| Target meaning | A system description draft was revised into a new version by a named member, awaiting review. |
| Selected source meaning | A system description draft was revised into a new version by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionDraftRevised.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionDraftRevised |
| Schema discriminator | bdgrz.systemdescription.system-description-draft-revised |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionDraftRevised.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionVersionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| VersionContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionDraftRevised.VersionContent.v1

| Property | Value |
| --- | --- |
| field | VersionContent |
| schema | SystemDescriptionDraftRevised.VersionContent.v1 |
| contentType | SystemDescriptionVersionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemDescriptionNoChangeRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionNoChangeRecorded |
| Selected source event name | SystemDescriptionNoChangeRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemDescriptionNoChangeRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionNoChangeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-no-change-recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionNoChangeRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-no-change-recorded","version":1}` |
| Target meaning | A non-material change in the period was recorded with a reasoned decision by a named member that the system description needs no update. |
| Selected source meaning | A non-material change in the period was recorded with a reasoned decision by a named member that the system description needs no update. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionNoChangeRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionNoChangeRecorded |
| Schema discriminator | bdgrz.systemdescription.system-description-no-change-recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionNoChangeRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionNoChangeDecisionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| DecisionContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionNoChangeRecorded.DecisionContent.v1

| Property | Value |
| --- | --- |
| field | DecisionContent |
| schema | SystemDescriptionNoChangeRecorded.DecisionContent.v1 |
| contentType | SystemDescriptionNoChangeDecisionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S020](../reactors/system-description-decision-reactor.md#s020) | physical root, logical prerequisite | SystemDescription | RecordSignificantChangeSystemDescriptionDecision | SignificantChange |
| [S021](../reactors/system-description-decision-reactor.md#s021) | physical root, logical prerequisite | SystemDescription | RecordComplianceIncidentSystemDescriptionDecision | ComplianceIncident |

### SystemDescriptionReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionReviewed |
| Selected source event name | SystemDescriptionReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemDescriptionReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionReviewed |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-reviewed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-reviewed","version":1}` |
| Target meaning | A system description version was reviewed with a decision by a named reviewer. |
| Selected source meaning | A system description version was reviewed with a decision by a named reviewer. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionReviewed |
| Schema discriminator | bdgrz.systemdescription.system-description-reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionReviewed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionReviewView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| ReviewContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionReviewed.ReviewContent.v1

| Property | Value |
| --- | --- |
| field | ReviewContent |
| schema | SystemDescriptionReviewed.ReviewContent.v1 |
| contentType | SystemDescriptionReviewView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemDescriptionSourcesRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionSourcesRecorded |
| Selected source event name | SystemDescriptionSourcesBound |
| Name decision | Names the current sources a member reconciled against a system description draft. |
| Canonical semantic key | event:SystemDescriptionSourcesBound |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionSourcesBound |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-sources-bound","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionSourcesBound.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-sources-bound","version":1}` |
| Target meaning | A new system description draft version was reconciled against its current sources by a named member, recording the sources the description now relies on. |
| Selected source meaning | A new system description draft version was reconciled against its current sources by a named member, recording the sources the description now relies on. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionSourcesBound.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionSourcesBound |
| Schema discriminator | bdgrz.systemdescription.system-description-sources-bound |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionSourcesBound.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionVersionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| VersionContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionSourcesBound.VersionContent.v1

| Property | Value |
| --- | --- |
| field | VersionContent |
| schema | SystemDescriptionSourcesBound.VersionContent.v1 |
| contentType | SystemDescriptionVersionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemDescriptionSuccessorProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemDescriptionSuccessorProposed |
| Selected source event name | SystemDescriptionSuccessorProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemDescriptionSuccessorProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemDescriptionSuccessorProposed |
| Adopted wire | `{"discriminator":"bdgrz.systemdescription.system-description-successor-proposed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionSuccessorProposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.systemdescription.system-description-successor-proposed","version":1}` |
| Target meaning | A successor draft of an approved system description was proposed with a new version by a named member, awaiting review. |
| Selected source meaning | A successor draft of an approved system description was proposed with a new version by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/SystemDescription/Events/SystemDescriptionSuccessorProposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | SystemDescriptionSuccessorProposed |
| Schema discriminator | bdgrz.systemdescription.system-description-successor-proposed |
| Schema discriminatorVersion | 1 |
| Schema owner | SystemDescription |
| Schema existingContract | src/Compliance.Common/Features/SystemDescription/SystemDescriptionSuccessorProposed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured SystemDescriptionVersionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| SystemDescriptionId | Uuid | true |
| AggregateRevision | long | true |
| VersionContent | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: SystemDescriptionSuccessorProposed.VersionContent.v1

| Property | Value |
| --- | --- |
| field | VersionContent |
| schema | SystemDescriptionSuccessorProposed.VersionContent.v1 |
| contentType | SystemDescriptionVersionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **SystemDescriptionApproved** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionBound** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionDraftCreated** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionDraftRevised** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionNoChangeRecorded** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionReviewed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionSourcesBound** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **SystemDescriptionSuccessorProposed** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
