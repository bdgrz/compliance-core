# CriterionApplicability events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/criterion-applicability.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| CriterionApplicabilityReviewed | business | bdgrz.criterion_applicability.reviewed | 3 | 14 | A proposal that a criterion does not apply to a program was reviewed with a decision outcome by a named actor, optionally under a separation-of-duties waiver. |
| CriterionNotApplicableProposed | business | bdgrz.criterion_applicability.not_applicable_proposed | 3 | 14 | A criterion of a criteria edition was proposed as not applicable to a program by a named member, awaiting review. |
| CriterionNotApplicableWithdrawn | business | bdgrz.criterion_applicability.not_applicable_withdrawn | 3 | 11 | A pending proposal that a criterion is not applicable to a program was withdrawn by a named actor, so the criterion stays applicable. |

## Event details

### CriterionApplicabilityReviewed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:CriterionApplicabilityReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CriterionApplicabilityReviewed |
| Adopted wire | `{"discriminator":"bdgrz.criterion_applicability.reviewed","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/ControlMappings/Events/CriterionApplicabilityReviewed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.criterion_applicability.reviewed","version":1}` |
| Meaning | A proposal that a criterion does not apply to a program was reviewed with a decision outcome by a named actor, optionally under a separation-of-duties waiver. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/ControlMappings/Events/CriterionApplicabilityReviewed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CriterionApplicabilityReviewed |
| Schema discriminator | bdgrz.criterion_applicability.reviewed |
| Schema discriminatorVersion | 1 |
| Schema owner | CriterionApplicability |
| Schema existingContract | src/Compliance.Common/Features/ControlMappings/CriterionApplicabilityReviewed.cs |
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
| DecisionId | Uuid | true |
| Revision | long | true |
| VersionNumber | int | true |
| ReviewDecisionId | Uuid | true |
| Outcome | string | true |
| Rationale | string | true |
| Actor | ActorReference | true |
| ReviewedAt | DateTimeOffset | true |
| SeparationOfDutiesWaiverId | Uuid? | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CriterionNotApplicableProposed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:CriterionNotApplicableProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CriterionNotApplicableProposed |
| Adopted wire | `{"discriminator":"bdgrz.criterion_applicability.not_applicable_proposed","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/ControlMappings/Events/CriterionNotApplicableProposed.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.criterion_applicability.not_applicable_proposed","version":1}` |
| Meaning | A criterion of a criteria edition was proposed as not applicable to a program by a named member, awaiting review. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/ControlMappings/Events/CriterionNotApplicableProposed.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CriterionNotApplicableProposed |
| Schema discriminator | bdgrz.criterion_applicability.not_applicable_proposed |
| Schema discriminatorVersion | 1 |
| Schema owner | CriterionApplicability |
| Schema existingContract | src/Compliance.Common/Features/ControlMappings/CriterionNotApplicableProposed.cs |
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
| DecisionId | Uuid | true |
| Revision | long | true |
| VersionNumber | int | true |
| EditionId | Uuid | true |
| CriterionIdentifier | string | true |
| Rationale | string | true |
| ProposerMemberId | Uuid | true |
| Actor | ActorReference | true |
| ProposedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### CriterionNotApplicableWithdrawn

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:CriterionNotApplicableWithdrawn |
| Owner selection | one exact canonical authority owner |
| Target concrete type | CriterionNotApplicableWithdrawn |
| Adopted wire | `{"discriminator":"bdgrz.criterion_applicability.not_applicable_withdrawn","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/ControlMappings/Events/CriterionNotApplicableWithdrawn.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.criterion_applicability.not_applicable_withdrawn","version":1}` |
| Meaning | A pending proposal that a criterion is not applicable to a program was withdrawn by a named actor, so the criterion stays applicable. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/ControlMappings/Events/CriterionNotApplicableWithdrawn.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | CriterionNotApplicableWithdrawn |
| Schema discriminator | bdgrz.criterion_applicability.not_applicable_withdrawn |
| Schema discriminatorVersion | 1 |
| Schema owner | CriterionApplicability |
| Schema existingContract | src/Compliance.Common/Features/ControlMappings/CriterionNotApplicableWithdrawn.cs |
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
| DecisionId | Uuid | true |
| Revision | long | true |
| VersionNumber | int | true |
| Rationale | string | true |
| Actor | ActorReference | true |
| WithdrawnAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **CriterionApplicabilityReviewed** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **CriterionNotApplicableProposed** — [Placeholder: reconcile schema version 1 with adopted version 3].
- **CriterionNotApplicableWithdrawn** — [Placeholder: reconcile schema version 1 with adopted version 3].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
