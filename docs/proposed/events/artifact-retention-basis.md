# ArtifactRetentionBasis events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/artifact-retention-basis.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ArtifactRetentionBasisRecorded | business | bdgrz.artifact_retention.basis_recorded | 1 | 14 | The retention basis for an artifact source was recorded: its policy, covered period, and the date it must be retained through, seven years after the period ends. |
| ArtifactRetentionBound | business | bdgrz.artifact_retention.bound | 1 | 8 | A retention binding was recorded for an artifact source under the earlier combined retention record, kept only as history; current retention decisions are recorded as separate basis and hold facts. |

## Event details

### ArtifactRetentionBasisRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactRetentionBasisRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactRetentionBasisRecorded |
| Adopted wire | `{"discriminator":"bdgrz.artifact_retention.basis_recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactRetentionBasisRecorded.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The retention basis for an artifact source was recorded: its policy, covered period, and the date it must be retained through, seven years after the period ends. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Retention/Events/ArtifactRetentionBasisRecorded.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactRetentionBasisRecorded |
| Schema discriminator | bdgrz.artifact_retention.basis_recorded |
| Schema discriminatorVersion | 1 |
| Schema owner | ArtifactRetentionBasis |
| Schema existingContract | src/Compliance.Common/Features/Retention/ArtifactRetentionBasisRecorded.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Source | ArtifactRetentionSource | true |
| Revision | long | true |
| SourcePosition | ulong | true |
| Policy | string | true |
| PeriodStart | DateOnly | true |
| PeriodEnd | DateOnly | true |
| RetainsThrough | DateOnly | true |
| BasisKind | string | true |
| Reason | string | true |
| RecordedBy | Bdgrz.Compliance.Features.AccessControl.ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactRetentionBound

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactRetentionBound |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactRetentionBound |
| Adopted wire | `{"discriminator":"bdgrz.artifact_retention.bound","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactRetentionBound.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A retention binding was recorded for an artifact source under the earlier combined retention record, kept only as history; current retention decisions are recorded as separate basis and hold facts. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Retention/Events/ArtifactRetentionBound.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactRetentionBound |
| Schema discriminator | bdgrz.artifact_retention.bound |
| Schema discriminatorVersion | 1 |
| Schema owner | ArtifactRetentionBasis |
| Schema existingContract | src/Compliance.Common/Features/Retention/ArtifactRetentionBound.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | existing native business fact, rehomed with its invariant |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| Source | ArtifactRetentionSource | true |
| Revision | long | true |
| SourcePosition | ulong | true |
| RecordedBy | Bdgrz.Compliance.Features.AccessControl.ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
