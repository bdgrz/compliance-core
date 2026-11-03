# ControlCriterionMapping events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/control-criterion-mapping.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ControlCriterionMappingProposed | ControlCriterionMappingProposed | business | bdgrz.control_mapping.proposed | 3 | 18 | A control version was proposed as satisfying a criterion of a criteria edition within a program, recorded as a mapping version by a named member, awaiting review. |
| ControlCriterionMappingRetired | ControlCriterionMappingRetired | business | bdgrz.control_mapping.retired | 3 | 12 | A control-to-criterion mapping was retired by a named actor, so the control no longer counts as satisfying that criterion from this version. |
| ControlCriterionMappingReviewed | ControlCriterionMappingReviewed | business | bdgrz.control_mapping.reviewed | 3 | 15 | A proposed control-to-criterion mapping version was reviewed with a decision outcome by a named reviewer, optionally under a separation-of-duties waiver. |

## Event details

### ControlCriterionMappingProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlCriterionMappingProposed |
| Selected source event name | ControlCriterionMappingProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlCriterionMappingProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlCriterionMappingProposed |
| Adopted wire | `{"discriminator":"bdgrz.control_mapping.proposed","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingProposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control_mapping.proposed","version":1}` |
| Target meaning | A control version was proposed as satisfying a criterion of a criteria edition within a program, recorded as a mapping version by a named member, awaiting review. |
| Selected source meaning | A control version was proposed as satisfying a criterion of a criteria edition within a program, recorded as a mapping version by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingProposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlCriterionMappingProposed |
| Schema discriminator | bdgrz.control_mapping.proposed |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlCriterionMapping |
| Schema existingContract | src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingProposed.cs |
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
| MappingId | Uuid | true |
| Revision | long | true |
| VersionNumber | int | true |
| ControlId | Uuid | true |
| ControlVersionId | Uuid | true |
| EditionId | Uuid | true |
| CriterionIdentifier | string | true |
| CriterionKind | string | true |
| Rationale | string | true |
| ApplicabilityExplanation | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| ProposedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlCriterionMappingRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlCriterionMappingRetired |
| Selected source event name | ControlCriterionMappingRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlCriterionMappingRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlCriterionMappingRetired |
| Adopted wire | `{"discriminator":"bdgrz.control_mapping.retired","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingRetired.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control_mapping.retired","version":1}` |
| Target meaning | A control-to-criterion mapping was retired by a named actor, so the control no longer counts as satisfying that criterion from this version. |
| Selected source meaning | A control-to-criterion mapping was retired by a named actor, so the control no longer counts as satisfying that criterion from this version. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingRetired.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlCriterionMappingRetired |
| Schema discriminator | bdgrz.control_mapping.retired |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlCriterionMapping |
| Schema existingContract | src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingRetired.cs |
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
| MappingId | Uuid | true |
| Revision | long | true |
| VersionNumber | int | true |
| Rationale | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| RetiredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ControlCriterionMappingReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ControlCriterionMappingReviewed |
| Selected source event name | ControlCriterionMappingReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ControlCriterionMappingReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ControlCriterionMappingReviewed |
| Adopted wire | `{"discriminator":"bdgrz.control_mapping.reviewed","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.control_mapping.reviewed","version":1}` |
| Target meaning | A proposed control-to-criterion mapping version was reviewed with a decision outcome by a named reviewer, optionally under a separation-of-duties waiver. |
| Selected source meaning | A proposed control-to-criterion mapping version was reviewed with a decision outcome by a named reviewer, optionally under a separation-of-duties waiver. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/ControlMappings/Events/ControlCriterionMappingReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ControlCriterionMappingReviewed |
| Schema discriminator | bdgrz.control_mapping.reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | ControlCriterionMapping |
| Schema existingContract | src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingReviewed.cs |
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
| MappingId | Uuid | true |
| Revision | long | true |
| VersionNumber | int | true |
| DecisionId | Uuid | true |
| Outcome | string | true |
| Rationale | string | true |
| ActorMemberId | Uuid | true |
| ActorDisplay | string | true |
| DecidedAt | DateTimeOffset | true |
| SeparationOfDutiesWaiverId | Uuid? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **ControlCriterionMappingProposed** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **ControlCriterionMappingRetired** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **ControlCriterionMappingReviewed** — [Placeholder: reconcile schema version 1 with adopted version 3].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
