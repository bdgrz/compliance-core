# RiskGovernance events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/risk-governance.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| RiskOwnerAssigned | RiskOwnerAssigned | business | bdgrz.risk.owner.assigned | 2 | 8 | A governed workforce person became the risk owner. OwnerContent is the complete RiskOwnerView. |
| RiskReassessmentTriggered | RiskReassessmentTriggered | business | bdgrz.risk.reassessment.triggered | 2 | 8 | A durable reassessment trigger was raised for the risk. TriggerContent is the complete RiskReassessmentTriggerView. |

## Event details

### RiskOwnerAssigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskOwnerAssigned |
| Selected source event name | RiskOwnerAssigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskOwnerAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskOwnerAssigned |
| Adopted wire | `{"discriminator":"bdgrz.risk.owner.assigned","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskOwnerAssigned.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.owner.assigned","version":1}` |
| Target meaning | A governed workforce person became the risk owner. OwnerContent is the complete RiskOwnerView. |
| Selected source meaning | A governed workforce person became the risk owner. OwnerContent is the complete RiskOwnerView. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskOwnerAssigned.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskOwnerAssigned |
| Schema discriminator | bdgrz.risk.owner.assigned |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskGovernance |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskOwnerAssigned.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured RiskOwnerView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| OwnerContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: RiskOwnerAssigned.OwnerContent.v1

| Property | Value |
| --- | --- |
| field | OwnerContent |
| schema | RiskOwnerAssigned.OwnerContent.v1 |
| contentType | RiskOwnerView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskReassessmentTriggered

| Canonical event detail | Value |
| --- | --- |
| Target event name | RiskReassessmentTriggered |
| Selected source event name | RiskReassessmentTriggered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RiskReassessmentTriggered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskReassessmentTriggered |
| Adopted wire | `{"discriminator":"bdgrz.risk.reassessment.triggered","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskReassessmentTriggered.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.reassessment.triggered","version":1}` |
| Target meaning | A durable reassessment trigger was raised for the risk. TriggerContent is the complete RiskReassessmentTriggerView. |
| Selected source meaning | A durable reassessment trigger was raised for the risk. TriggerContent is the complete RiskReassessmentTriggerView. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskReassessmentTriggered.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskReassessmentTriggered |
| Schema discriminator | bdgrz.risk.reassessment.triggered |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskGovernance |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskReassessmentTriggered.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured RiskReassessmentTriggerView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RiskId | Uuid | true |
| Revision | long | true |
| TriggerContent | ImmutableContentRef | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: RiskReassessmentTriggered.TriggerContent.v1

| Property | Value |
| --- | --- |
| field | TriggerContent |
| schema | RiskReassessmentTriggered.TriggerContent.v1 |
| contentType | RiskReassessmentTriggerView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S036](../reactors/risk-method-reassessment-reactor.md#s036) | emitted semantic fact | RiskMethod | RaiseRiskReassessmentTriggers | RiskGovernance |
| [S037](../reactors/boundary-risk-reassessment-reactor.md#s037) | emitted semantic fact | SystemBoundary | RaiseRiskReassessmentTriggers | RiskGovernance |
| [X009](../reactors/risk-evaluation-reactor.md#x009) | emitted semantic fact | RiskEvaluation | RecordRiskReassessmentObligation | RiskGovernance |


## Wire/schema version discrepancies

- **RiskOwnerAssigned** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskReassessmentTriggered** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
