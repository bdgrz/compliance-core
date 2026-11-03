# TrainingRequirement events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/training-requirement.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TrainingRequirementDefined | TrainingRequirementDefined | business | bdgrz.training_requirement.defined | 2 | 13 | Defines a requirement's first version or an immutable successor version. ContentContent is the complete TrainingRequirementContent; DefineRequestId is the logical operation identity that defined this version. |

## Event details

### TrainingRequirementDefined

| Canonical event detail | Value |
| --- | --- |
| Target event name | TrainingRequirementDefined |
| Selected source event name | TrainingRequirementDefined |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TrainingRequirementDefined |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TrainingRequirementDefined |
| Adopted wire | `{"discriminator":"bdgrz.training_requirement.defined","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/PolicyDistribution/Events/TrainingRequirementDefined.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.training_requirement.defined","version":1}` |
| Target meaning | Defines a requirement's first version or an immutable successor version. ContentContent is the complete TrainingRequirementContent; DefineRequestId is the logical operation identity that defined this version. |
| Selected source meaning | Defines a requirement's first version or an immutable successor version. ContentContent is the complete TrainingRequirementContent; DefineRequestId is the logical operation identity that defined this version. |
| Meaning source | record summary in src/Compliance.Common/Features/PolicyDistribution/Events/TrainingRequirementDefined.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TrainingRequirementDefined |
| Schema discriminator | bdgrz.training_requirement.defined |
| Schema discriminatorVersion | 1 |
| Schema owner | TrainingRequirement |
| Schema existingContract | src/Compliance.Common/Features/PolicyDistribution/TrainingRequirementDefined.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize structured TrainingRequirementContent including nested collections; a former item cap cannot become an event-size rejection for valid larger content. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| RequirementId | Uuid | true |
| Identifier | string | true |
| Version | long | true |
| ContentContent | ImmutableContentRef | true |
| ContentSha256 | string | true |
| DefineRequestId | Uuid | true |
| Actor | ActorReference | true |
| ChangedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: TrainingRequirementDefined.ContentContent.v1

| Property | Value |
| --- | --- |
| field | ContentContent |
| schema | TrainingRequirementDefined.ContentContent.v1 |
| contentType | TrainingRequirementContent |
| rule | Complete original typed structured value, including nested collections, written/verified before this event. No cumulative parent view or mutable latest pointer. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]


## Wire/schema version discrepancies

- **TrainingRequirementDefined** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
