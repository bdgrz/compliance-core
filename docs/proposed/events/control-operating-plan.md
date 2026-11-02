# ControlOperatingPlan events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/control-operating-plan.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ControlOperatingPlanApproved | business | bdgrz.control.operating_plan.approved | 2 | 14 | An independent approval that puts a pending plan version into effect. The complete list of open work that moves to the approved owner is verified immutable content, so no item can be truncated. |
| ControlOperatingPlanProposed | business | bdgrz.control.operating_plan.proposed | 2 | 8 | A pending plan version on one control's plan line. The logical id of Operation is the immutable plan version id; the complete ControlOperatingPlanView is verified immutable content. |

## Event details

### ControlOperatingPlanApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlOperatingPlanApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlOperatingPlanApproved |
| Adopted wire | `{"discriminator":"bdgrz.control.operating_plan.approved","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/ControlOperatingPlanApproved.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.operating_plan.approved","version":1}` |
| Meaning | An independent approval that puts a pending plan version into effect. The complete list of open work that moves to the approved owner is verified immutable content, so no item can be truncated. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/ControlOperatingPlanApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlOperatingPlanApproved |
| Schema discriminator | bdgrz.control.operating_plan.approved |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlOperatingPlan |
| Schema existingContract | src/Compliance.Common/Features/Operations/ControlOperatingPlanApproved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<OccurrenceReassignmentView> to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| Revision | long | true |
| PlanVersionId | Uuid | true |
| ApproverMemberId | Uuid | true |
| ApprovedBy | ActorReference | true |
| ApprovedAt | DateTimeOffset | true |
| Rationale | string | true |
| SeparationOfDutiesWaiverId | Uuid? | true |
| ReassignmentsManifest | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlOperatingPlanApproved.ReassignmentsManifest.v1

| Property | Value |
| --- | --- |
| field | ReassignmentsManifest |
| schema | ControlOperatingPlanApproved.ReassignmentsManifest.v1 |
| contentType | IReadOnlyList<OccurrenceReassignmentView> |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlOperatingPlanProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ControlOperatingPlanProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlOperatingPlanProposed |
| Adopted wire | `{"discriminator":"bdgrz.control.operating_plan.proposed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Operations/Events/ControlOperatingPlanProposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control.operating_plan.proposed","version":1}` |
| Meaning | A pending plan version on one control's plan line. The logical id of Operation is the immutable plan version id; the complete ControlOperatingPlanView is verified immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/Operations/Events/ControlOperatingPlanProposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlOperatingPlanProposed |
| Schema discriminator | bdgrz.control.operating_plan.proposed |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlOperatingPlan |
| Schema existingContract | src/Compliance.Common/Features/Operations/ControlOperatingPlanProposed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured ControlOperatingPlanView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| ControlId | Uuid | true |
| Revision | long | true |
| PlanContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: ControlOperatingPlanProposed.PlanContent.v1

| Property | Value |
| --- | --- |
| field | PlanContent |
| schema | ControlOperatingPlanProposed.PlanContent.v1 |
| contentType | ControlOperatingPlanView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **ControlOperatingPlanApproved** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **ControlOperatingPlanProposed** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
