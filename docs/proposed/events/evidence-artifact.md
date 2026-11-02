# EvidenceArtifact events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/evidence-artifact.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ArtifactDownloadAvailabilityUseSettled | reservation | bdgrz.evidence-artifact.artifact-download-availability-use-settled | 1 | 8 | null (recorded) |
| EvidenceArtifactDownloadRequested | business | bdgrz.evidence-artifact.evidence-artifact-download-requested | 1 | 8 | null (recorded) |
| EvidenceArtifactInspected | business | bdgrz.evidence.artifact.inspected | 1 | [Placeholder: schema absent] | Records the inspection result that moves an artifact out of pending inspection. |
| EvidenceArtifactQuarantineReleased | business | bdgrz.evidence.artifact.quarantine_released | 1 | [Placeholder: schema absent] | A quarantined evidence artifact was released after a clean rescan, with a rationale and the deciding actor recording that the malware detection was a false positive. |
| EvidenceArtifactRegistered | business | bdgrz.evidence.artifact.registered | 1 | [Placeholder: schema absent] | Registers uploaded content as evidence awaiting inspection; the content is not yet available. |

## Event details

### ArtifactDownloadAvailabilityUseSettled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDownloadAvailabilityUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDownloadAvailabilityUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.evidence-artifact.artifact-download-availability-use-settled","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactDownloadAvailabilityUseSettled |
| Schema discriminator | bdgrz.evidence-artifact.artifact-download-availability-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceArtifact |
| Schema existingContract | null (recorded) |
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
| [T104](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t104) | emitted semantic fact | ArtifactDownloadGrant | SettleArtifactDownloadAvailabilityUse | EvidenceArtifact |

### EvidenceArtifactDownloadRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceArtifactDownloadRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactDownloadRequested |
| Adopted wire | `{"discriminator":"bdgrz.evidence-artifact.evidence-artifact-download-requested","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceArtifactDownloadRequested |
| Schema discriminator | bdgrz.evidence-artifact.evidence-artifact-download-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceArtifact |
| Schema existingContract | null (recorded) |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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
| [T099](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t099) | logical prerequisite | EvidenceArtifact | AuthorizeAdvisorArtifactDownloadUse | AdvisorHandoff |
| [T100](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t100) | logical prerequisite | EvidenceArtifact | AuthorizeArtifactHandlingDownloadUse | EvidenceHandlingPolicy |
| [T101](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t101) | logical prerequisite | EvidenceArtifact | IssueProtectedArtifactDownloadGrant | ArtifactDownloadGrant |
| [A021](../reactors/unassigned-architecture-rows.md#a021) | physical root, logical prerequisite | EvidenceArtifact | CancelProtectedArtifactDownloadGrant | ArtifactDownloadGrant |
| [P000](../reactors/unassigned-architecture-rows.md#p000) | emitted semantic fact | null (recorded) | IssueArtifactDownload | EvidenceArtifact |
| [Q000](../reactors/unassigned-architecture-rows.md#q000) | logical prerequisite | null (recorded) | GetArtifactDownloadGrant | ArtifactDownloadGrant |

### EvidenceArtifactInspected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceArtifactInspected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactInspected |
| Adopted wire | `{"discriminator":"bdgrz.evidence.artifact.inspected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactInspected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the inspection result that moves an artifact out of pending inspection. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactInspected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S000](../reactors/evidence-artifact-inspection-effects-reactor.md#s000) | physical root, logical prerequisite | EvidenceArtifact | null (recorded) | null (recorded) |

### EvidenceArtifactQuarantineReleased

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceArtifactQuarantineReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactQuarantineReleased |
| Adopted wire | `{"discriminator":"bdgrz.evidence.artifact.quarantine_released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactQuarantineReleased.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A quarantined evidence artifact was released after a clean rescan, with a rationale and the deciding actor recording that the malware detection was a false positive. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactQuarantineReleased.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### EvidenceArtifactRegistered

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceArtifactRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactRegistered |
| Adopted wire | `{"discriminator":"bdgrz.evidence.artifact.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Registers uploaded content as evidence awaiting inspection; the content is not yet available. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactRegistered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
