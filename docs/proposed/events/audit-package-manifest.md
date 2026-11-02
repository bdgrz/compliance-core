# AuditPackageManifest events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/audit-package-manifest.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditPackageAmended | business | bdgrz.auditpackages.audit-package-amended | 1 | 9 | An audit package was amended into its next manifest version by an accepted change, which may follow a frozen version without altering the earlier version. |
| AuditPackageBuildRequested | business | bdgrz.auditpackages.audit-package-build-requested | 1 | 9 | A build of the archive for one exact manifest version of an audit package was admitted, binding the build to that version and permitting the builder to report a result. |
| AuditPackageBuilt | business | bdgrz.auditpackages.audit-package-built | 1 | 9 | The archive for an audit package version was built and its verified output recorded, answering the admitted build request for that exact version. |
| AuditPackageCreated | business | bdgrz.auditpackages.audit-package-created | 1 | 9 | An audit package was created for a program as its first manifest version. |
| AuditPackageDeliveryAdmitted | business | bdgrz.audit-package-manifest.audit-package-delivery-admitted | 1 | 8 | Freezes an exact personal external-handoff attestation before independent historical materialization. |
| AuditPackageDisclosureApproved | business | bdgrz.auditpackages.audit-package-disclosure-approved | 1 | 9 | Disclosure of one audit package manifest version to its recipients was approved, permitting that version to be admitted for delivery. |
| AuditPackageValidated | business | bdgrz.auditpackages.audit-package-validated | 1 | 9 | The outcome of validating one audit package manifest version was recorded against that version. |
| AuditPackageValidationOverrideApproved | business | bdgrz.auditpackages.audit-package-validation-override-approved | 1 | 9 | An approver overrode the validation findings for one audit package manifest version, allowing that version to proceed despite them. |

## Event details

### AuditPackageAmended

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageAmended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageAmended |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-amended","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageAmended.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An audit package was amended into its next manifest version by an accepted change, which may follow a frozen version without altering the earlier version. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageAmended.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageAmended |
| Schema discriminator | bdgrz.auditpackages.audit-package-amended |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageAmended.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: AuditPackageAmended.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditPackageAmended.change.v1 |
| sourceCommandVariants | AmendAuditPackage |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPackageBuildRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageBuildRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageBuildRequested |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-build-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageBuildRequested.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A build of the archive for one exact manifest version of an audit package was admitted, binding the build to that version and permitting the builder to report a result. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageBuildRequested.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageBuildRequested |
| Schema discriminator | bdgrz.auditpackages.audit-package-build-requested |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageBuildRequested.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: package-build-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | package-build-intent.v1 |
| recordType | PackageBuildIntent |
| rule | BuildId uses existing BuildNamespace UUIDv5 over package/version/trimmed BuildKey. This is logical build identity, not worker attempt. Freeze complete manifest/selection before queueing; worker replay reads only this content. No build may alter a delivered version. |
| sourceCommandVariants | RequestAuditPackageBuild |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| PackageId | Uuid | true |
| ExaminationId | Uuid | true |
| PackageKind | string | true |
| PackageVersion | long | true |
| BuildId | Uuid | true |
| BuildKey | string | true |
| ManifestId | Uuid | true |
| ManifestSha256 | string | true |
| Manifest | ImmutableContentRef | true |
| Selection | ImmutableSelectionManifest | true |

##### Variant: PackageBuildIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| PackageId | Uuid | true |
| ExaminationId | Uuid | true |
| PackageKind | string | true |
| PackageVersion | long | true |
| BuildId | Uuid | true |
| BuildKey | string | true |
| ManifestId | Uuid | true |
| ManifestSha256 | string | true |
| Manifest | ImmutableContentRef | true |
| Selection | ImmutableSelectionManifest | true |


#### Resolved intent schema

PackageBuildIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T031](../reactors/audit-package-build-process-reactor.md#t031) | physical root, logical prerequisite | AuditPackageManifest | StartAuditPackageBuild | AuditPackageBuildProcess |
| [T032](../reactors/audit-package-build-process-reactor.md#t032) | physical root | AuditPackageBuildProcess | RecordAuditPackageBuildResult | AuditPackageManifest |

### AuditPackageBuilt

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageBuilt |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageBuilt |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-built","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageBuilt.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The archive for an audit package version was built and its verified output recorded, answering the admitted build request for that exact version. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageBuilt.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageBuilt |
| Schema discriminator | bdgrz.auditpackages.audit-package-built |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageBuilt.cs |
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

#### Content schema: package-build-intent.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | package-build-intent.v1 |
| recordType | PackageBuildIntent |
| rule | BuildId uses existing BuildNamespace UUIDv5 over package/version/trimmed BuildKey. This is logical build identity, not worker attempt. Freeze complete manifest/selection before queueing; worker replay reads only this content. No build may alter a delivered version. |
| sourceCommandVariants | RecordAuditPackageBuildResult |

##### Fields

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| PackageId | Uuid | true |
| ExaminationId | Uuid | true |
| PackageKind | string | true |
| PackageVersion | long | true |
| BuildId | Uuid | true |
| BuildKey | string | true |
| ManifestId | Uuid | true |
| ManifestSha256 | string | true |
| Manifest | ImmutableContentRef | true |
| Selection | ImmutableSelectionManifest | true |

##### Variant: PackageBuildIntent

| Field | Type | Required |
| --- | --- | --- |
| Header | DurableIntentHeader | true |
| PackageId | Uuid | true |
| ExaminationId | Uuid | true |
| PackageKind | string | true |
| PackageVersion | long | true |
| BuildId | Uuid | true |
| BuildKey | string | true |
| ManifestId | Uuid | true |
| ManifestSha256 | string | true |
| Manifest | ImmutableContentRef | true |
| Selection | ImmutableSelectionManifest | true |


#### Resolved intent schema

PackageBuildIntent

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T032](../reactors/audit-package-build-process-reactor.md#t032) | emitted semantic fact | AuditPackageBuildProcess | RecordAuditPackageBuildResult | AuditPackageManifest |

### AuditPackageCreated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageCreated |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-created","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageCreated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An audit package was created for a program as its first manifest version. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageCreated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageCreated |
| Schema discriminator | bdgrz.auditpackages.audit-package-created |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageCreated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: AuditPackageCreated.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditPackageCreated.change.v1 |
| sourceCommandVariants | CreateAuditPackage |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPackageDeliveryAdmitted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageDeliveryAdmitted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageDeliveryAdmitted |
| Adopted wire | `{"discriminator":"bdgrz.audit-package-manifest.audit-package-delivery-admitted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDeliveryAdmitted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Freezes an exact personal external-handoff attestation before independent historical materialization. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDeliveryAdmitted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageDeliveryAdmitted |
| Schema discriminator | bdgrz.audit-package-manifest.audit-package-delivery-admitted |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
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

#### Content schema: package-delivery-recording-intent.v1

| Property | Value |
| --- | --- |
| field | Intent |
| schema | package-delivery-recording-intent.v1 |
| recordType | PackageDeliveryRecordingIntent |
| rule | Personal HTTP attestation of an external handoff. DeliveryId uses existing DeliveryNamespace algorithm. Check current exact manifest/archive/validation/recipient approval atomically at manifest admission; freeze version then. Authored DeliveredAt is distinct from RecordedAt. Child fact records the admitted attestation; no send adapter is invoked. |

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
| [T081](../reactors/audit-package-delivery-admission-reactor.md#t081) | physical root, logical prerequisite | AuditPackageManifest | MaterializeRecordedAuditPackageDelivery | AuditPackageDelivery |

### AuditPackageDisclosureApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageDisclosureApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageDisclosureApproved |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-disclosure-approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDisclosureApproved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Disclosure of one audit package manifest version to its recipients was approved, permitting that version to be admitted for delivery. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageDisclosureApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageDisclosureApproved |
| Schema discriminator | bdgrz.auditpackages.audit-package-disclosure-approved |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageDisclosureApproved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: AuditPackageDisclosureApproved.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditPackageDisclosureApproved.change.v1 |
| sourceCommandVariants | ApproveAuditPackageDisclosure |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPackageValidated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageValidated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageValidated |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-validated","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageValidated.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The outcome of validating one audit package manifest version was recorded against that version. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageValidated.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageValidated |
| Schema discriminator | bdgrz.auditpackages.audit-package-validated |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageValidated.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: AuditPackageValidated.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditPackageValidated.change.v1 |
| sourceCommandVariants | ValidateAuditPackage |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPackageValidationOverrideApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPackageValidationOverrideApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPackageValidationOverrideApproved |
| Adopted wire | `{"discriminator":"bdgrz.auditpackages.audit-package-validation-override-approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AuditPackages/Events/AuditPackageValidationOverrideApproved.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An approver overrode the validation findings for one audit package manifest version, allowing that version to proceed despite them. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AuditPackages/Events/AuditPackageValidationOverrideApproved.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | AuditPackageValidationOverrideApproved |
| Schema discriminator | bdgrz.auditpackages.audit-package-validation-override-approved |
| Schema discriminatorVersion | 1 |
| Schema owner | AuditPackageManifest |
| Schema existingContract | src/Compliance.Common/Features/AuditPackages/AuditPackageValidationOverrideApproved.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
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

#### Content schema: AuditPackageValidationOverrideApproved.change.v1

| Property | Value |
| --- | --- |
| field | Change |
| schema | AuditPackageValidationOverrideApproved.change.v1 |
| sourceCommandVariants | ApproveAuditPackageValidationOverride |
| rule | Tagged by stable source command kind. Contains normalized accepted business field values and exact source proof references from that command, plus server-accepted result identities; excludes unrelated children, history, bearer material and process state. The result data required for replay is immutable and complete. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
