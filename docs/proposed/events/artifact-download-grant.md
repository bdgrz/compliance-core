# ArtifactDownloadGrant events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/artifact-download-grant.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ArtifactDownloadGrantCanceled | ArtifactDownloadGrantCanceled | business | bdgrz.artifact-download-grant.artifact-download-grant-canceled | 1 | 9 | An artifact download grant was canceled before its source use was settled, so the grant can never be issued or used to download the artifact. |
| EvidenceArtifactDownloadIssued | EvidenceArtifactDownloadIssued | business | bdgrz.evidence.evidence-artifact-download-issued | 2 | 14 | Records the receiving grant for one protected evidence artifact download. |

## Event details

### ArtifactDownloadGrantCanceled

| Canonical event detail | Value |
| --- | --- |
| Target event name | ArtifactDownloadGrantCanceled |
| Selected source event name | ArtifactDownloadGrantCanceled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ArtifactDownloadGrantCanceled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDownloadGrantCanceled |
| Adopted wire | `{"discriminator":"bdgrz.artifact-download-grant.artifact-download-grant-canceled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/ArtifactDownloadGrantCanceled.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An artifact download grant was canceled before its source use was settled, so the grant can never be issued or used to download the artifact. |
| Selected source meaning | An artifact download grant was canceled before its source use was settled, so the grant can never be issued or used to download the artifact. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Evidence/Events/ArtifactDownloadGrantCanceled.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactDownloadGrantCanceled |
| Schema discriminator | bdgrz.artifact-download-grant.artifact-download-grant-canceled |
| Schema discriminatorVersion | 1 |
| Schema owner | ArtifactDownloadGrant |
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
| GrantId | Uuid | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: evidence-download-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | evidence-download-intent.v1 |
| recordType | EvidenceDownloadIntent |
| rule | GrantId equals root CommandId. Admit/pin available inspected artifact, then acquire complete optional handoff and explicit handling-policy uses. No fabricated use for an absent optional source; default immutable policy version is frozen. Original ticket lives only in protected result content. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| GrantId | Uuid | true |
| ArtifactId | Uuid | true |
| ContentSha256 | string | true |
| ContentLength | long | true |
| ArtifactRevision | ulong | true |
| Purpose | string | true |
| ProgramId | Uuid? | true |
| AdvisorHandoffId | Uuid? | true |
| SelectedSource | AdvisorRecordVersion? | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| MaxDeliveryLifetime | TimeSpan | true |

##### Variant: EvidenceDownloadIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| GrantId | Uuid | true |
| ArtifactId | Uuid | true |
| ContentSha256 | string | true |
| ContentLength | long | true |
| ArtifactRevision | ulong | true |
| Purpose | string | true |
| ProgramId | Uuid? | true |
| AdvisorHandoffId | Uuid? | true |
| SelectedSource | AdvisorRecordVersion? | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| MaxDeliveryLifetime | TimeSpan | true |


#### Resolved intent schema

EvidenceDownloadIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T102](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t102) | logical prerequisite | ArtifactDownloadGrant | CloseAdvisorArtifactDownloadAuthorization | AdvisorHandoff |
| [T103](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t103) | logical prerequisite | ArtifactDownloadGrant | CloseArtifactHandlingDownloadAuthorization | EvidenceHandlingPolicy |
| [T104](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t104) | logical prerequisite | ArtifactDownloadGrant | CloseArtifactDownloadAvailabilityAuthorization | EvidenceArtifact |
| [A021](../reactors/unassigned-domain-choreography.md#a021) | emitted semantic fact | EvidenceArtifact | CancelProtectedArtifactDownloadGrant | ArtifactDownloadGrant |
| [Q000](../reactors/unassigned-domain-choreography.md#q000) | logical prerequisite | [Placeholder: value not recorded] | GetArtifactDownloadGrant | ArtifactDownloadGrant |

### EvidenceArtifactDownloadIssued

| Canonical event detail | Value |
| --- | --- |
| Target event name | EvidenceArtifactDownloadIssued |
| Selected source event name | EvidenceArtifactDownloadIssued |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EvidenceArtifactDownloadIssued |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactDownloadIssuedV2 |
| Adopted wire | `{"discriminator":"bdgrz.evidence.evidence-artifact-download-issued","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactDownloadIssuedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | selected_native_schema_with_adopted_explicit_wire_v2_owner_migration |
| Source status | declared |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | `{"discriminator":"bdgrz.evidence.evidence-artifact-download-issued","version":2,"concreteType":"EvidenceArtifactDownloadIssuedV2","owner":"ArtifactDownloadGrant"}` |
| Target meaning | Records the receiving grant for one protected evidence artifact download. |
| Selected source meaning | Records the receiving grant for one protected evidence artifact download. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactDownloadIssuedV2.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceArtifactDownloadIssued |
| Schema discriminator | bdgrz.evidence.evidence-artifact-download-issued |
| Schema discriminatorVersion | 1 |
| Schema owner | ArtifactDownloadGrant |
| Schema existingContract | src/Compliance.Common/Features/Evidence/EvidenceArtifactDownloadIssued.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | None listed |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ArtifactId | Uuid | true |
| Revision | long | true |
| RequestId | Uuid | true |
| ContentSha256 | string | true |
| ContentLength | long | true |
| Purpose | string | true |
| IssuedBy | ActorReference | true |
| IssuedAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| Intent | ImmutableContentRef | true |
| GrantId | Uuid | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: evidence-download-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | evidence-download-intent.v1 |
| recordType | EvidenceDownloadIntent |
| rule | GrantId equals root CommandId. Admit/pin available inspected artifact, then acquire complete optional handoff and explicit handling-policy uses. No fabricated use for an absent optional source; default immutable policy version is frozen. Original ticket lives only in protected result content. |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| GrantId | Uuid | true |
| ArtifactId | Uuid | true |
| ContentSha256 | string | true |
| ContentLength | long | true |
| ArtifactRevision | ulong | true |
| Purpose | string | true |
| ProgramId | Uuid? | true |
| AdvisorHandoffId | Uuid? | true |
| SelectedSource | AdvisorRecordVersion? | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| MaxDeliveryLifetime | TimeSpan | true |

##### Variant: EvidenceDownloadIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| GrantId | Uuid | true |
| ArtifactId | Uuid | true |
| ContentSha256 | string | true |
| ContentLength | long | true |
| ArtifactRevision | ulong | true |
| Purpose | string | true |
| ProgramId | Uuid? | true |
| AdvisorHandoffId | Uuid? | true |
| SelectedSource | AdvisorRecordVersion? | true |
| RequiredSourceUses | ImmutableSelectionManifest | true |
| MaxDeliveryLifetime | TimeSpan | true |


#### Resolved intent schema

EvidenceDownloadIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T101](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t101) | emitted semantic fact | EvidenceArtifact | IssueProtectedArtifactDownloadGrant | ArtifactDownloadGrant |
| [T102](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t102) | logical prerequisite | ArtifactDownloadGrant | CloseAdvisorArtifactDownloadAuthorization | AdvisorHandoff |
| [T103](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t103) | logical prerequisite | ArtifactDownloadGrant | CloseArtifactHandlingDownloadAuthorization | EvidenceHandlingPolicy |
| [T104](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t104) | logical prerequisite | ArtifactDownloadGrant | CloseArtifactDownloadAvailabilityAuthorization | EvidenceArtifact |
| [Q000](../reactors/unassigned-domain-choreography.md#q000) | logical prerequisite | [Placeholder: value not recorded] | GetArtifactDownloadGrant | ArtifactDownloadGrant |


## Wire/schema version discrepancies

- **EvidenceArtifactDownloadIssued** — [Placeholder: reconcile schema version 1 with adopted version 2].

[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
