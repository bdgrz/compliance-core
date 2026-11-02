# AuditPackageDelivery events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/audit-package-delivery.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditPackageDelivered | business | bdgrz.auditpackages.audit-package-delivered | 1 | 9 | The independently materialized historical handoff with its immutable original terminal result. |

## Event details

### AuditPackageDelivered

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageDelivered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageDelivered |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-delivered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDelivered.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The independently materialized historical handoff with its immutable original terminal result. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDelivered.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageDelivered |
| Schema discriminator | bdgrz.auditpackages.audit-package-delivered |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageDelivery |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageDelivered.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | typed reactor receiver accepted business fact |
| Schema changes | Replace full parent snapshot with a verified immutable event-specific change. Reducer applies the accepted delta; it never re-executes a command or consults foreign state. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SubjectId | Uuid | true |
| Revision | long | true |
| ActorMemberId | Uuid? | true |
| Change | ImmutableContentRef | true |
| OccurredAt | DateTimeOffset | true |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: package-delivery-recording-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | package-delivery-recording-intent.v1 |
| recordType | PackageDeliveryRecordingIntent |
| rule | Personal HTTP attestation of an external handoff. DeliveryId uses existing DeliveryNamespace algorithm. Check current exact manifest/archive/validation/recipient approval atomically at manifest admission; freeze version then. Authored DeliveredAt is distinct from RecordedAt. Child fact records the admitted attestation; no send adapter is invoked. |
| sourceCommandVariants | MaterializeRecordedAuditPackageDelivery |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DeliveryId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | long | true |
| ManifestId | Uuid | true |
| ManifestSha256 | string | true |
| ArchiveArtifactId | Uuid | true |
| ArchiveSha256 | string | true |
| Recipient | string | true |
| DeliveryReference | string | true |
| DeliveredAt | DateTimeOffset | true |
| RecordedAt | DateTimeOffset | true |
| RecorderMemberId | Uuid | true |
| DisclosureApproval | SourceFactLocator | true |
| ValidationProofs | ImmutableSelectionManifest | true |

##### Variant: PackageDeliveryRecordingIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| DeliveryId | Uuid | true |
| PackageId | Uuid | true |
| PackageVersion | long | true |
| ManifestId | Uuid | true |
| ManifestSha256 | string | true |
| ArchiveArtifactId | Uuid | true |
| ArchiveSha256 | string | true |
| Recipient | string | true |
| DeliveryReference | string | true |
| DeliveredAt | DateTimeOffset | true |
| RecordedAt | DateTimeOffset | true |
| RecorderMemberId | Uuid | true |
| DisclosureApproval | SourceFactLocator | true |
| ValidationProofs | ImmutableSelectionManifest | true |


#### Resolved intent schema

PackageDeliveryRecordingIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T081](../reactors/audit-package-delivery-admission-reactor.md#t081) | emitted semantic fact | AuditPackageManifest | MaterializeRecordedAuditPackageDelivery | AuditPackageDelivery |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
