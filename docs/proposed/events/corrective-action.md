# CorrectiveAction events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/corrective-action.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| CorrectiveActionAdded | CorrectiveActionAddedV2 | business | bdgrz.finding.corrective_action.added | 3 | 10 | The materialization of an admitted corrective action on its own owner, at revision one. The complete CorrectiveActionView is verified content and Intent repeats the admission intent the finding accepted. |
| CorrectiveActionCompleted | CorrectiveActionCompletedV2 | business | bdgrz.finding.corrective_action.completed | 3 | 14 | The completion of one corrective action on its own owner. EvidenceManifest is the complete evidence list as verified immutable content, with every item and its order preserved. |
| CorrectiveActionFindingClosureAuthorized | CorrectiveActionFindingClosureUseAuthorized | reservation | bdgrz.corrective-action.corrective-action-finding-closure-use-authorized | 1 | 8 | A completed corrective action is held for a finding closure. The use does not advance the public action revision: Revision is the held revision, and Generation is the finding generation the closure seals. |
| CorrectiveActionFindingClosureAuthorizationClosed | CorrectiveActionFindingClosureUseSettled | reservation | bdgrz.corrective-action.corrective-action-finding-closure-use-settled | 1 | 8 | A finding-closure use of a corrective action ended. Disposition repeats the disposition of the settlement intent so the owner can install the closed-parent fence from its own ordered stream: a SourceUseSettlementDisposition.ParentClosed settlement keeps the action under the closed finding generation, and releasing the temporary use never restores an unrestricted writable action. |

## Event details

### CorrectiveActionAdded

| Canonical event detail | Value |
| --- | --- |
| Target event name | CorrectiveActionAdded |
| Selected source event name | CorrectiveActionAddedV2 |
| Name decision | Names the creation of the admitted corrective action without exposing the implementation version suffix. |
| Canonical semantic key | event:CorrectiveActionAdded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CorrectiveActionAddedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.corrective_action.added","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionAddedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.corrective_action.added","version":1}` |
| Target meaning | The materialization of an admitted corrective action on its own owner, at revision one. The complete CorrectiveActionView is verified content and Intent repeats the admission intent the finding accepted. |
| Selected source meaning | The materialization of an admitted corrective action on its own owner, at revision one. The complete CorrectiveActionView is verified content and Intent repeats the admission intent the finding accepted. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionAddedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CorrectiveActionAddedV2 |
| Schema discriminator | bdgrz.finding.corrective_action.added |
| Schema discriminatorVersion | 1 |
| Schema owner | CorrectiveAction |
| Schema existingContract | src/Compliance.Common/Features/Remediation/CorrectiveActionAddedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Externalize structured CorrectiveActionView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
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
| ActionContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: CorrectiveActionAddedV2.ActionContent.v1

| Property | Value |
| --- | --- |
| field | ActionContent |
| schema | CorrectiveActionAddedV2.ActionContent.v1 |
| contentType | CorrectiveActionView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |

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
| [T036](../reactors/open-corrective-action-for-finding-reactor.md#t036) | emitted semantic fact | FindingAggregate | CreateCorrectiveAction | CorrectiveAction |

### CorrectiveActionCompleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | CorrectiveActionCompleted |
| Selected source event name | CorrectiveActionCompletedV2 |
| Name decision | Names the completed corrective action without exposing the implementation version suffix. |
| Canonical semantic key | event:CorrectiveActionCompleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CorrectiveActionCompletedV2 |
| Adopted wire | `{"discriminator":"bdgrz.finding.corrective_action.completed","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionCompletedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.finding.corrective_action.completed","version":1}` |
| Target meaning | The completion of one corrective action on its own owner. EvidenceManifest is the complete evidence list as verified immutable content, with every item and its order preserved. |
| Selected source meaning | The completion of one corrective action on its own owner. EvidenceManifest is the complete evidence list as verified immutable content, with every item and its order preserved. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionCompletedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CorrectiveActionCompletedV2 |
| Schema discriminator | bdgrz.finding.corrective_action.completed |
| Schema discriminatorVersion | 1 |
| Schema owner | CorrectiveAction |
| Schema existingContract | src/Compliance.Common/Features/Remediation/CorrectiveActionCompletedV2.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<EvidenceReference> to verified immutable content; preserve all items/order/identity, no truncation. |
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
| ActionId | Uuid | true |
| ResolutionNotes | string | true |
| EvidenceManifest | ImmutableContentRef | true |
| CompletedByMemberId | Uuid | true |
| CompletedBy | ActorReference | true |
| CompletedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: CorrectiveActionCompletedV2.EvidenceManifest.v1

| Property | Value |
| --- | --- |
| field | EvidenceManifest |
| schema | CorrectiveActionCompletedV2.EvidenceManifest.v1 |
| contentType | IReadOnlyList<EvidenceReference> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CorrectiveActionFindingClosureAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | CorrectiveActionFindingClosureAuthorized |
| Selected source event name | CorrectiveActionFindingClosureUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:CorrectiveActionFindingClosureUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CorrectiveActionFindingClosureUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.corrective-action.corrective-action-finding-closure-use-authorized","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionFindingClosureUseAuthorized.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A completed corrective action is held for a finding closure. The use does not advance the public action revision: Revision is the held revision, and Generation is the finding generation the closure seals. |
| Selected source meaning | A completed corrective action is held for a finding closure. The use does not advance the public action revision: Revision is the held revision, and Generation is the finding generation the closure seals. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionFindingClosureUseAuthorized.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CorrectiveActionFindingClosureUseAuthorized |
| Schema discriminator | bdgrz.corrective-action.corrective-action-finding-closure-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | CorrectiveAction |
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

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T063](../reactors/finding-closure-reactor.md#t063) | emitted semantic fact | FindingAggregate | AuthorizeCorrectiveActionFindingClosure | CorrectiveAction |

### CorrectiveActionFindingClosureAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | CorrectiveActionFindingClosureAuthorizationClosed |
| Selected source event name | CorrectiveActionFindingClosureUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:CorrectiveActionFindingClosureUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CorrectiveActionFindingClosureUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.corrective-action.corrective-action-finding-closure-use-settled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionFindingClosureUseSettled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A finding-closure use of a corrective action ended. Disposition repeats the disposition of the settlement intent so the owner can install the closed-parent fence from its own ordered stream: a SourceUseSettlementDisposition.ParentClosed settlement keeps the action under the closed finding generation, and releasing the temporary use never restores an unrestricted writable action. |
| Selected source meaning | A finding-closure use of a corrective action ended. Disposition repeats the disposition of the settlement intent so the owner can install the closed-parent fence from its own ordered stream: a SourceUseSettlementDisposition.ParentClosed settlement keeps the action under the closed finding generation, and releasing the temporary use never restores an unrestricted writable action. |
| Meaning source | record summary in src/Compliance.Common/Features/Remediation/Events/CorrectiveActionFindingClosureUseSettled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CorrectiveActionFindingClosureUseSettled |
| Schema discriminator | bdgrz.corrective-action.corrective-action-finding-closure-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | CorrectiveAction |
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

#### Content schema: source-use-authorization-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | source-use-authorization-intent.v1 |
| recordType | SourceUseAuthorizationIntent |
| rule | A use fact points to the immutable original workflow intent plus this exact source-specific requirement/token. No arbitrary command snapshot serves as policy proof. Terminal settlement before grant fences the same key; neither lease expiry nor missing read releases it. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |

##### Variant: SourceUseAuthorizationIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| ParentIntent | ImmutableContentRef | true |
| Reservation | ReservationKey | true |
| Requirement | SourceUseRequirement | true |
| Grant | ReservationGrant? | true |
| Disposition | SourceUseSettlementDisposition? | true |


#### Resolved intent schema

SourceUseAuthorizationIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T064](../reactors/finding-closure-reactor.md#t064) | emitted semantic fact | FindingAggregate | CloseCorrectiveActionFindingClosureAuthorization | CorrectiveAction |


## Wire/schema version discrepancies

- **CorrectiveActionAddedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **CorrectiveActionCompletedV2** — [Placeholder: reconcile schema version 1 with adopted version 3].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
