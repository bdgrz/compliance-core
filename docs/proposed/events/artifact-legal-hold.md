# ArtifactLegalHold events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/artifact-legal-hold.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ArtifactLegalHoldPlaced | business | bdgrz.artifact_retention.legal_hold_placed | 1 | 10 | A legal hold was placed on a retained artifact source with a stated reason by a named actor, blocking its disposal until the hold is released. |
| ArtifactLegalHoldReleased | business | bdgrz.artifact_retention.legal_hold_released | 1 | 10 | A previously placed legal hold on a retained artifact source was released with a stated reason, allowing the artifact to be disposed once its retention period ends. |

## Event details

### ArtifactLegalHoldPlaced

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactLegalHoldPlaced |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactLegalHoldPlaced |
| Adopted wire | `{"discriminator":"bdgrz.artifact_retention.legal_hold_placed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactLegalHoldPlaced.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A legal hold was placed on a retained artifact source with a stated reason by a named actor, blocking its disposal until the hold is released. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Retention/Events/ArtifactLegalHoldPlaced.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactLegalHoldPlaced |
| Schema discriminator | bdgrz.artifact_retention.legal_hold_placed |
| Schema discriminatorVersion | 1 |
| Schema owner | ArtifactLegalHold |
| Schema existingContract | src/Compliance.Common/Features/Retention/ArtifactLegalHoldPlaced.cs |
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
| HoldId | Uuid | true |
| Reason | string | true |
| RecordedBy | Bdgrz.Compliance.Features.AccessControl.ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X006](../reactors/artifact-legal-hold-reactor.md#x006) | physical root, logical prerequisite | ArtifactLegalHold | FenceArtifactDispositionForHold | ArtifactDisposition |

### ArtifactLegalHoldReleased

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactLegalHoldReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactLegalHoldReleased |
| Adopted wire | `{"discriminator":"bdgrz.artifact_retention.legal_hold_released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactLegalHoldReleased.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A previously placed legal hold on a retained artifact source was released with a stated reason, allowing the artifact to be disposed once its retention period ends. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Retention/Events/ArtifactLegalHoldReleased.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactLegalHoldReleased |
| Schema discriminator | bdgrz.artifact_retention.legal_hold_released |
| Schema discriminatorVersion | 1 |
| Schema owner | ArtifactLegalHold |
| Schema existingContract | src/Compliance.Common/Features/Retention/ArtifactLegalHoldReleased.cs |
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
| HoldId | Uuid | true |
| Reason | string | true |
| RecordedBy | Bdgrz.Compliance.Features.AccessControl.ActorReference | true |
| RecordedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X006](../reactors/artifact-legal-hold-reactor.md#x006) | physical root, logical prerequisite | ArtifactLegalHold | FenceArtifactDispositionForHold | ArtifactDisposition |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
