# EvidenceArtifact events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/evidence-artifact.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ArtifactDownloadAvailabilityAuthorizationClosed | ArtifactDownloadAvailabilityUseSettled | reservation | bdgrz.evidence-artifact.artifact-download-availability-use-settled | 1 | 8 | The artifact ended the temporary availability hold for one exact download after the file was issued or the request was canceled. |
| EvidenceArtifactDownloadRequested | EvidenceArtifactDownloadRequested | business | bdgrz.evidence-artifact.evidence-artifact-download-requested | 1 | 8 | A member requested a specific evidence file and recorded the purpose for downloading it. The file is made available only after its handling rules are satisfied. |
| EvidenceArtifactInspected | EvidenceArtifactInspected | business | bdgrz.evidence.artifact.inspected | 1 | [Placeholder: schema absent] | Records the inspection result that moves an artifact out of pending inspection. |
| EvidenceArtifactQuarantineReleased | EvidenceArtifactQuarantineReleased | business | bdgrz.evidence.artifact.quarantine_released | 1 | [Placeholder: schema absent] | A quarantined evidence artifact was released after a clean rescan, with a rationale and the deciding actor recording that the malware detection was a false positive. |
| EvidenceArtifactRegistered | EvidenceArtifactRegistered | business | bdgrz.evidence.artifact.registered | 1 | [Placeholder: schema absent] | Registers uploaded content as evidence awaiting inspection; the content is not yet available. |

## Event details

### ArtifactDownloadAvailabilityAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ArtifactDownloadAvailabilityAuthorizationClosed |
| Selected source event name | ArtifactDownloadAvailabilityUseSettled |
| Name decision | Names the completed end of the artifact’s availability decision for one download. |
| Canonical semantic key | event:ArtifactDownloadAvailabilityUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDownloadAvailabilityUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.evidence-artifact.artifact-download-availability-use-settled","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The artifact ended the temporary availability hold for one exact download after the file was issued or the request was canceled. |
| Selected source meaning | [Placeholder: value not recorded] |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactDownloadAvailabilityUseSettled |
| Schema discriminator | bdgrz.evidence-artifact.artifact-download-availability-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceArtifact |
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
| [T104](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t104) | emitted semantic fact | ArtifactDownloadGrant | CloseArtifactDownloadAvailabilityAuthorization | EvidenceArtifact |

### EvidenceArtifactDownloadRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | EvidenceArtifactDownloadRequested |
| Selected source event name | EvidenceArtifactDownloadRequested |
| Name decision | Names a member’s completed request to download a specific evidence artifact. |
| Canonical semantic key | event:EvidenceArtifactDownloadRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactDownloadRequested |
| Adopted wire | `{"discriminator":"bdgrz.evidence-artifact.evidence-artifact-download-requested","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A member requested a specific evidence file and recorded the purpose for downloading it. The file is made available only after its handling rules are satisfied. |
| Selected source meaning | [Placeholder: value not recorded] |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | EvidenceArtifactDownloadRequested |
| Schema discriminator | bdgrz.evidence-artifact.evidence-artifact-download-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceArtifact |
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
| [T099](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t099) | logical prerequisite | EvidenceArtifact | AuthorizeAdvisorArtifactDownload | AdvisorHandoff |
| [T100](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t100) | logical prerequisite | EvidenceArtifact | AuthorizeArtifactHandlingDownload | EvidenceHandlingPolicy |
| [T101](../reactors/fulfill-authorized-artifact-download-request-reactor.md#t101) | logical prerequisite | EvidenceArtifact | IssueProtectedArtifactDownloadGrant | ArtifactDownloadGrant |
| [A021](../reactors/unassigned-domain-choreography.md#a021) | physical root, logical prerequisite | EvidenceArtifact | CancelProtectedArtifactDownloadGrant | ArtifactDownloadGrant |
| [P000](../reactors/unassigned-domain-choreography.md#p000) | emitted semantic fact | [Placeholder: value not recorded] | IssueArtifactDownload | EvidenceArtifact |
| [Q000](../reactors/unassigned-domain-choreography.md#q000) | logical prerequisite | [Placeholder: value not recorded] | GetArtifactDownloadGrant | ArtifactDownloadGrant |

### EvidenceArtifactInspected

| Canonical event detail | Value |
| --- | --- |
| Target event name | EvidenceArtifactInspected |
| Selected source event name | EvidenceArtifactInspected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EvidenceArtifactInspected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactInspected |
| Adopted wire | `{"discriminator":"bdgrz.evidence.artifact.inspected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactInspected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the inspection result that moves an artifact out of pending inspection. |
| Selected source meaning | Records the inspection result that moves an artifact out of pending inspection. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactInspected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S000](../reactors/apply-artifact-inspection-outcome-reactor.md#s000) | physical root, logical prerequisite | EvidenceArtifact | [Placeholder: value not recorded] | [Placeholder: value not recorded] |

### EvidenceArtifactQuarantineReleased

| Canonical event detail | Value |
| --- | --- |
| Target event name | EvidenceArtifactQuarantineReleased |
| Selected source event name | EvidenceArtifactQuarantineReleased |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EvidenceArtifactQuarantineReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactQuarantineReleased |
| Adopted wire | `{"discriminator":"bdgrz.evidence.artifact.quarantine_released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactQuarantineReleased.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A quarantined evidence artifact was released after a clean rescan, with a rationale and the deciding actor recording that the malware detection was a false positive. |
| Selected source meaning | A quarantined evidence artifact was released after a clean rescan, with a rationale and the deciding actor recording that the malware detection was a false positive. |
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
| Target event name | EvidenceArtifactRegistered |
| Selected source event name | EvidenceArtifactRegistered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:EvidenceArtifactRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceArtifactRegistered |
| Adopted wire | `{"discriminator":"bdgrz.evidence.artifact.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Registers uploaded content as evidence awaiting inspection; the content is not yet available. |
| Selected source meaning | Registers uploaded content as evidence awaiting inspection; the content is not yet available. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceArtifactRegistered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
