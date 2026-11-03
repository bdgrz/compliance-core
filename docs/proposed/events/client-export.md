# ClientExport events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/client-export.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ClientExportAttestPackageDeliveryRecorded | ClientExportAttestPackageDeliveryRecorded | business | bdgrz.exports.client-export-attest-package-delivery-recorded | 1 | [Placeholder: schema absent] | A delivery decision for an attest package was recorded on a successfully built client export, with its rationale, recipient and delivery reference, for the exact package manifest. |
| ClientExportBuildFailed | ClientExportBuildFailed | business | bdgrz.exports.client-export-build-failed | 2 | [Placeholder: schema absent] | Records the ClientExportBuildFailed domain fact for one tenant-owned subject revision. |
| ClientExportBuilt | ClientExportBuilt | business | bdgrz.exports.client-export-built | 2 | [Placeholder: schema absent] | Records the ClientExportBuilt domain fact for one tenant-owned subject revision. |
| ClientExportDelivered | ClientExportDelivered | business | bdgrz.exports.client-export-delivered | 2 | [Placeholder: schema absent] | Records the ClientExportDelivered domain fact for one tenant-owned subject revision. |
| ClientExportRequested | ClientExportRequested | business | bdgrz.exports.client-export-requested | 2 | [Placeholder: schema absent] | Records the ClientExportRequested domain fact for one tenant-owned subject revision. |

## Event details

### ClientExportAttestPackageDeliveryRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientExportAttestPackageDeliveryRecorded |
| Selected source event name | ClientExportAttestPackageDeliveryRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientExportAttestPackageDeliveryRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientExportAttestPackageDeliveryRecorded |
| Adopted wire | `{"discriminator":"bdgrz.exports.client-export-attest-package-delivery-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Exports/Events/ClientExportAttestPackageDeliveryRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A delivery decision for an attest package was recorded on a successfully built client export, with its rationale, recipient and delivery reference, for the exact package manifest. |
| Selected source meaning | A delivery decision for an attest package was recorded on a successfully built client export, with its rationale, recipient and delivery reference, for the exact package manifest. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Exports/Events/ClientExportAttestPackageDeliveryRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X007](../reactors/client-export-delivery-reactor.md#x007) | physical root, logical prerequisite | ClientExport | RecordEngagementPackageDelivery | ArtifactEngagementHold |

### ClientExportBuildFailed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientExportBuildFailed |
| Selected source event name | ClientExportBuildFailed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientExportBuildFailed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientExportBuildFailed |
| Adopted wire | `{"discriminator":"bdgrz.exports.client-export-build-failed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Exports/Events/ClientExportBuildFailed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ClientExportBuildFailed domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ClientExportBuildFailed domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Exports/Events/ClientExportBuildFailed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ClientExportBuilt

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientExportBuilt |
| Selected source event name | ClientExportBuilt |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientExportBuilt |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientExportBuilt |
| Adopted wire | `{"discriminator":"bdgrz.exports.client-export-built","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Exports/Events/ClientExportBuilt.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ClientExportBuilt domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ClientExportBuilt domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Exports/Events/ClientExportBuilt.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ClientExportDelivered

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientExportDelivered |
| Selected source event name | ClientExportDelivered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientExportDelivered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientExportDelivered |
| Adopted wire | `{"discriminator":"bdgrz.exports.client-export-delivered","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Exports/Events/ClientExportDelivered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ClientExportDelivered domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ClientExportDelivered domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Exports/Events/ClientExportDelivered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ClientExportRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientExportRequested |
| Selected source event name | ClientExportRequested |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientExportRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientExportRequested |
| Adopted wire | `{"discriminator":"bdgrz.exports.client-export-requested","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Exports/Events/ClientExportRequested.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ClientExportRequested domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ClientExportRequested domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Exports/Events/ClientExportRequested.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
