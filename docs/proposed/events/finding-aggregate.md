# FindingAggregate events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/finding-aggregate.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| CorrectiveActionAdmitted | business | bdgrz.finding-aggregate.corrective-action-admitted | 1 | 8 | The finding accepted one corrective action for the current finding generation. Intent is the frozen ChildAdmissionIntent that carries the complete action; the action owner is materialized independently and the admission, not the materialization, is the authority. The logical id of Operation is the action id. |
| FindingAcceptanceLinkedV2 | business | bdgrz.finding.acceptance.linked | 3 | 9 | A risk acceptance or waiver linked to the finding; the complete FindingAcceptanceView is verified content. |
| FindingClosedV2 | business | bdgrz.finding.closed | 3 | 12 | The finding closed. ClosureContent is the complete FindingClosureView and ActionCompletionProofs the complete manifest of the held completion facts of every admitted action of FindingGeneration. |
| FindingClosureAccepted | business | bdgrz.finding-aggregate.finding-closure-accepted | 1 | 8 | The root native acceptance of a closure decision. It seals action admission for Generation and references the frozen ClosureIntent; the finding only closes once FindingClosedV2 follows. |
| FindingClosureCanceled | business | bdgrz.finding-aggregate.finding-closure-canceled | 1 | 8 | A pending closure ended before it closed the finding. The fact is the local receiver fence: a delayed CompleteFindingClosure sees it and does nothing, and only then are the action uses settled. SourceProofs is the failure proof. |
| FindingRaisedV2 | business | bdgrz.finding.raised | 3 | 20 | The root native fact of a finding. LinksManifest is the complete original link list as verified immutable content, so a large list is never an event-size rejection. |
| FindingReopenedV2 | business | bdgrz.finding.reopened | 3 | 11 | A closed finding was reopened. The finding advances to the next generation; admitted actions and their history are kept, and a closure accepted for an earlier generation can no longer close it. |
| FindingRevisedV2 | business | bdgrz.finding.revised | 3 | 16 | An attributable severity, ownership, due-date, scope or root-cause change. |

## Event details

### CorrectiveActionAdmitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:CorrectiveActionAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CorrectiveActionAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.finding-aggregate.corrective-action-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The finding accepted one corrective action for the current finding generation. Intent is the frozen ChildAdmissionIntent that carries the complete action; the action owner is materialized independently and the admission, not the materialization, is the authority. The logical id of Operation is the action id. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CorrectiveActionAdmitted |
| Schema discriminator | bdgrz.finding-aggregate.corrective-action-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
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

#### Content schema: child-admission-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | child-admission-intent.v1 |
| recordType | ChildAdmissionIntent |
| rule | Concrete child payload schemas are the corresponding normalized Common requests: advisor feedback, auditor response/comment, corrective action, policy acknowledgement/training/waiver. Preserve all authored fields and original actor; child reducer never loads live parent. No later close/revoke discards admitted work. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| ChildKind | AdmittedChildKind | true |
| ChildId | Uuid | true |
| CanonicalAuthoredContent | ImmutableContentRef | true |
| SelectedSourceProofs | ImmutableSelectionManifest | true |
| ParentAdmissionRevision | ulong | true |

##### Variant: ChildAdmissionIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| ChildKind | AdmittedChildKind | true |
| ChildId | Uuid | true |
| CanonicalAuthoredContent | ImmutableContentRef | true |
| SelectedSourceProofs | ImmutableSelectionManifest | true |
| ParentAdmissionRevision | ulong | true |


#### Resolved intent schema

ChildAdmissionIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T036](../reactors/corrective-action-materialization-reactor.md#t036) | physical root, logical prerequisite | FindingAggregate | MaterializeCorrectiveAction | CorrectiveAction |

### FindingAcceptanceLinkedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingAcceptanceLinked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingAcceptanceLinkedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.acceptance.linked","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/FindingAcceptanceLinkedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.acceptance.linked","version":1}` |
| Meaning | A risk acceptance or waiver linked to the finding; the complete FindingAcceptanceView is verified content. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingAcceptanceLinkedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingAcceptanceLinkedV2 |
| Schema discriminator | bdgrz.finding.acceptance.linked |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
| Schema existingContract | src/Compliance.Common/Features/Remediation/FindingAcceptanceLinkedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured FindingAcceptanceView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| RequestId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| Revision | long | true |
| AcceptanceContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: FindingAcceptanceLinkedV2.AcceptanceContent.v1

| Property | Value |
| --- | --- |
| field | AcceptanceContent |
| schema | FindingAcceptanceLinkedV2.AcceptanceContent.v1 |
| contentType | FindingAcceptanceView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### FindingClosedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingClosed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingClosedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.closed","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/FindingClosedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.closed","version":1}` |
| Meaning | The finding closed. ClosureContent is the complete FindingClosureView and ActionCompletionProofs the complete manifest of the held completion facts of every admitted action of FindingGeneration. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingClosedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingClosedV2 |
| Schema discriminator | bdgrz.finding.closed |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
| Schema existingContract | src/Compliance.Common/Features/Remediation/FindingClosedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured FindingClosureView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| RequestId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| Revision | long | true |
| ClosureContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FindingGeneration | long | true |
| ActionCompletionProofs | ImmutableContentRef | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: FindingClosedV2.ClosureContent.v1

| Property | Value |
| --- | --- |
| field | ClosureContent |
| schema | FindingClosedV2.ClosureContent.v1 |
| contentType | FindingClosureView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T037](../reactors/finding-closure-reactor.md#t037) | emitted semantic fact | FindingAggregate | CompleteFindingClosure | FindingAggregate |
| [T064](../reactors/finding-closure-reactor.md#t064) | logical prerequisite | FindingAggregate | SettleCorrectiveActionFindingClosureUse | CorrectiveAction |

### FindingClosureAccepted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingClosureAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingClosureAccepted |
| Adopted wire | `{"discriminator":"bdgrz.finding-aggregate.finding-closure-accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Remediation/Events/FindingClosureAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The root native acceptance of a closure decision. It seals action admission for Generation and references the frozen ClosureIntent; the finding only closes once FindingClosedV2 follows. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingClosureAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingClosureAccepted |
| Schema discriminator | bdgrz.finding-aggregate.finding-closure-accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
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

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T037](../reactors/finding-closure-reactor.md#t037) | physical root, logical prerequisite | FindingAggregate | CompleteFindingClosure | FindingAggregate |
| [T063](../reactors/finding-closure-reactor.md#t063) | physical root, logical prerequisite | FindingAggregate | AuthorizeCorrectiveActionFindingClosureUse | CorrectiveAction |
| [T064](../reactors/finding-closure-reactor.md#t064) | physical root | FindingAggregate | SettleCorrectiveActionFindingClosureUse | CorrectiveAction |
| [A008](../reactors/unassigned-architecture-rows.md#a008) | physical root, logical prerequisite | FindingAggregate | AbortFindingClosure | FindingAggregate |

### FindingClosureCanceled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingClosureCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingClosureCanceled |
| Adopted wire | `{"discriminator":"bdgrz.finding-aggregate.finding-closure-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Remediation/Events/FindingClosureCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A pending closure ended before it closed the finding. The fact is the local receiver fence: a delayed CompleteFindingClosure sees it and does nothing, and only then are the action uses settled. SourceProofs is the failure proof. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingClosureCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingClosureCanceled |
| Schema discriminator | bdgrz.finding-aggregate.finding-closure-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
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

#### Content schema: closure-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | closure-intent.v1 |
| recordType | ClosureIntent |
| rule | Seal parent admission, drain all previously admitted materializations, then grant complete exact child/source uses. Nonempty action/response requirements and accepted dispositions remain policy-specific. An abort atomically fences parent finalization before any source settlement. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |

##### Variant: ClosureIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentKind | ClosureParentKind | true |
| ParentId | Uuid | true |
| ParentGeneration | ulong | true |
| AdmittedChildren | ImmutableSelectionManifest | true |
| AuthoredDecision | ImmutableContentRef | true |
| SourceUses | ImmutableSelectionManifest | true |
| WaiverProof | ImmutableContentRef? | true |


#### Resolved intent schema

ClosureIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T064](../reactors/finding-closure-reactor.md#t064) | logical prerequisite | FindingAggregate | SettleCorrectiveActionFindingClosureUse | CorrectiveAction |
| [A008](../reactors/unassigned-architecture-rows.md#a008) | emitted semantic fact | FindingAggregate | AbortFindingClosure | FindingAggregate |

### FindingRaisedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingRaised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingRaisedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.raised","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/FindingRaisedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.raised","version":1}` |
| Meaning | The root native fact of a finding. LinksManifest is the complete original link list as verified immutable content, so a large list is never an event-size rejection. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingRaisedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingRaisedV2 |
| Schema discriminator | bdgrz.finding.raised |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
| Schema existingContract | src/Compliance.Common/Features/Remediation/FindingRaisedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<FindingLink> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| RequestId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| Revision | long | true |
| Source | FindingSource | true |
| Title | string | true |
| Description | string | true |
| Severity | string | true |
| AffectedScope | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| LinksManifest | ImmutableContentRef | true |
| RaisedBy | ActorReference | true |
| RaisedAt | DateTimeOffset | true |
| CreationKind | string | false |
| InitialActionSha256 | string? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: FindingRaisedV2.LinksManifest.v1

| Property | Value |
| --- | --- |
| field | LinksManifest |
| schema | FindingRaisedV2.LinksManifest.v1 |
| contentType | IReadOnlyList<FindingLink> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S001](../reactors/control-evaluation-deviation-finding-reactor.md#s001) | emitted semantic fact | ControlEvaluation | RaiseEvaluationDeviationFinding | FindingAggregate |
| [S002](../reactors/control-occurrence-finding-reactor.md#s002) | emitted semantic fact | ControlOccurrence | RaiseOccurrenceFinding | FindingAggregate |

### FindingReopenedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingReopened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingReopenedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.reopened","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/FindingReopenedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.reopened","version":1}` |
| Meaning | A closed finding was reopened. The finding advances to the next generation; admitted actions and their history are kept, and a closure accepted for an earlier generation can no longer close it. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingReopenedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingReopenedV2 |
| Schema discriminator | bdgrz.finding.reopened |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
| Schema existingContract | src/Compliance.Common/Features/Remediation/FindingReopenedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| RequestId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| Revision | long | true |
| Reason | string | true |
| ReopenedBy | ActorReference | true |
| ReopenedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### FindingRevisedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:FindingRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | FindingRevisedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.revised","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/FindingRevisedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.revised","version":1}` |
| Meaning | An attributable severity, ownership, due-date, scope or root-cause change. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/FindingRevisedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | FindingRevisedV2 |
| Schema discriminator | bdgrz.finding.revised |
| Schema discriminatorVersion | 1 |
| Schema owner | FindingAggregate |
| Schema existingContract | src/Compliance.Common/Features/Remediation/FindingRevisedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| RequestId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| FindingId | Uuid | true |
| Revision | long | true |
| Severity | string | true |
| OwnerMemberId | Uuid | true |
| DueOn | DateOnly | true |
| AffectedScope | string | true |
| RootCause | string? | true |
| Reason | string | true |
| RevisedBy | ActorReference | true |
| RevisedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **FindingAcceptanceLinkedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **FindingClosedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **FindingRaisedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **FindingReopenedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **FindingRevisedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
