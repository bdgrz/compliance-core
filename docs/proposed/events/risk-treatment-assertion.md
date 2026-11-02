# RiskTreatmentAssertion events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/risk-treatment-assertion.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| RiskControlTreatmentProposed | business | bdgrz.risk.control_treatment.proposed | 2 | 9 | A control treatment assertion was proposed on the (risk, control) line. TreatmentContent is the complete proposed RiskControlTreatmentView; the logical id of the operation is the treatment id. |
| RiskControlTreatmentRetired | business | bdgrz.risk.control_treatment.retired | 2 | 11 | An accepted control treatment assertion was withdrawn; its history is kept. |
| RiskControlTreatmentReviewed | business | bdgrz.risk.control_treatment.reviewed | 2 | 14 | An independent reviewer accepted or rejected the pending control treatment assertion of the line. |

## Event details

### RiskControlTreatmentProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RiskControlTreatmentProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskControlTreatmentProposed |
| Adopted wire | `{"discriminator":"bdgrz.risk.control_treatment.proposed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentProposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.control_treatment.proposed","version":1}` |
| Meaning | A control treatment assertion was proposed on the (risk, control) line. TreatmentContent is the complete proposed RiskControlTreatmentView; the logical id of the operation is the treatment id. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentProposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskControlTreatmentProposed |
| Schema discriminator | bdgrz.risk.control_treatment.proposed |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAssertion |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskControlTreatmentProposed.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured RiskControlTreatmentView including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
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
| TreatmentContent | ImmutableContentRef | true |
| ProposerMemberId | Uuid | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: RiskControlTreatmentProposed.TreatmentContent.v1

| Property | Value |
| --- | --- |
| field | TreatmentContent |
| schema | RiskControlTreatmentProposed.TreatmentContent.v1 |
| contentType | RiskControlTreatmentView |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskControlTreatmentRetired

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RiskControlTreatmentRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskControlTreatmentRetired |
| Adopted wire | `{"discriminator":"bdgrz.risk.control_treatment.retired","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentRetired.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.control_treatment.retired","version":1}` |
| Meaning | An accepted control treatment assertion was withdrawn; its history is kept. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentRetired.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskControlTreatmentRetired |
| Schema discriminator | bdgrz.risk.control_treatment.retired |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAssertion |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskControlTreatmentRetired.cs |
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
| RiskId | Uuid | true |
| Revision | long | true |
| TreatmentId | Uuid | true |
| Rationale | string | true |
| RetiredBy | ActorReference | true |
| RetiredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### RiskControlTreatmentReviewed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RiskControlTreatmentReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RiskControlTreatmentReviewed |
| Adopted wire | `{"discriminator":"bdgrz.risk.control_treatment.reviewed","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.risk.control_treatment.reviewed","version":1}` |
| Meaning | An independent reviewer accepted or rejected the pending control treatment assertion of the line. |
| Meaning source | record summary in src/Compliance.Common/Features/Risks/Events/RiskControlTreatmentReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | RiskControlTreatmentReviewed |
| Schema discriminator | bdgrz.risk.control_treatment.reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | RiskTreatmentAssertion |
| Schema existingContract | src/Compliance.Common/Features/Risks/RiskControlTreatmentReviewed.cs |
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
| RiskId | Uuid | true |
| Revision | long | true |
| TreatmentId | Uuid | true |
| DecisionId | Uuid | true |
| Outcome | string | true |
| Rationale | string | true |
| Reviewer | ActorReference | true |
| ReviewedAt | DateTimeOffset | true |
| SeparationOfDutiesWaiverId | Uuid? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **RiskControlTreatmentProposed** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskControlTreatmentRetired** — [Placeholder: reconcile schema version 1 with adopted version 2].
- **RiskControlTreatmentReviewed** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
