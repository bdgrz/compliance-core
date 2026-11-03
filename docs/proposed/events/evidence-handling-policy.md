# EvidenceHandlingPolicy events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/evidence-handling-policy.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ArtifactHandlingDownloadAuthorized | ArtifactHandlingDownloadUseAuthorized | reservation | bdgrz.evidence-handling-policy.artifact-handling-download-use-authorized | 1 | 8 | null (recorded) |
| ArtifactHandlingDownloadAuthorizationClosed | ArtifactHandlingDownloadUseSettled | reservation | bdgrz.evidence-handling-policy.artifact-handling-download-use-settled | 1 | 8 | null (recorded) |
| ArtifactHandlingPolicyAssigned | ArtifactHandlingPolicyAssigned | business | bdgrz.retention.artifact-handling-policy-assigned | 1 | [Placeholder: schema absent] | Records the ArtifactHandlingPolicyAssigned domain fact for one tenant-owned subject revision. |
| ArtifactHandlingPolicyRevised | ArtifactHandlingPolicyRevised | business | bdgrz.retention.artifact-handling-policy-revised | 1 | [Placeholder: schema absent] | Records a changed, attributed handling classification for an exact artifact identity. |

## Event details

### ArtifactHandlingDownloadAuthorized

| Canonical event detail | Value |
| --- | --- |
| Target event name | ArtifactHandlingDownloadAuthorized |
| Selected source event name | ArtifactHandlingDownloadUseAuthorized |
| Name decision | Authorization is already the completed fact; Use adds no stakeholder meaning. |
| Canonical semantic key | event:ArtifactHandlingDownloadUseAuthorized |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactHandlingDownloadUseAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.evidence-handling-policy.artifact-handling-download-use-authorized","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | null (recorded) |
| Selected source meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactHandlingDownloadUseAuthorized |
| Schema discriminator | bdgrz.evidence-handling-policy.artifact-handling-download-use-authorized |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceHandlingPolicy |
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
| [T100](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t100) | emitted semantic fact | EvidenceArtifact | AuthorizeArtifactHandlingDownload | EvidenceHandlingPolicy |

### ArtifactHandlingDownloadAuthorizationClosed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ArtifactHandlingDownloadAuthorizationClosed |
| Selected source event name | ArtifactHandlingDownloadUseSettled |
| Name decision | The exact authorization ended; the recorded outcome still distinguishes use, cancellation, or a late fence. |
| Canonical semantic key | event:ArtifactHandlingDownloadUseSettled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactHandlingDownloadUseSettled |
| Adopted wire | `{"discriminator":"bdgrz.evidence-handling-policy.artifact-handling-download-use-settled","version":1}` |
| Wire qualification | Exact current/selected wire shape remains subject to documented final-schema reconciliation; semantic owner/name is fixed. |
| Schema status | selected_complete_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | ARTIFACT_DOWNLOAD |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | null (recorded) |
| Selected source meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | ArtifactHandlingDownloadUseSettled |
| Schema discriminator | bdgrz.evidence-handling-policy.artifact-handling-download-use-settled |
| Schema discriminatorVersion | 1 |
| Schema owner | EvidenceHandlingPolicy |
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
| [T103](../reactors/one-committed-evidence-artifact-download-requested-starter.md#t103) | emitted semantic fact | ArtifactDownloadGrant | CloseArtifactHandlingDownloadAuthorization | EvidenceHandlingPolicy |

### ArtifactHandlingPolicyAssigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | ArtifactHandlingPolicyAssigned |
| Selected source event name | ArtifactHandlingPolicyAssigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ArtifactHandlingPolicyAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactHandlingPolicyAssigned |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-handling-policy-assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactHandlingPolicyAssigned.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ArtifactHandlingPolicyAssigned domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ArtifactHandlingPolicyAssigned domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactHandlingPolicyAssigned.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactHandlingPolicyRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | ArtifactHandlingPolicyRevised |
| Selected source event name | ArtifactHandlingPolicyRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ArtifactHandlingPolicyRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactHandlingPolicyRevised |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-handling-policy-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactHandlingPolicyRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records a changed, attributed handling classification for an exact artifact identity. |
| Selected source meaning | Records a changed, attributed handling classification for an exact artifact identity. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactHandlingPolicyRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
