# ArtifactEngagementHold events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/artifact-engagement-hold.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ArtifactEngagementHoldPlaced | business | bdgrz.retention.artifact-engagement-hold-placed | 1 | [Placeholder: schema absent] | Records the ArtifactEngagementHoldPlaced domain fact for one tenant-owned subject revision. |
| ArtifactEngagementHoldReleased | business | bdgrz.retention.artifact-engagement-hold-released | 1 | [Placeholder: schema absent] | Records the ArtifactEngagementHoldReleased domain fact for one tenant-owned subject revision. |
| EngagementReportIssued | business | bdgrz.retention.engagement-report-issued | 1 | [Placeholder: schema absent] | Records the EngagementReportIssued domain fact for one tenant-owned subject revision. |

## Event details

### ArtifactEngagementHoldPlaced

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactEngagementHoldPlaced |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactEngagementHoldPlaced |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-engagement-hold-placed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactEngagementHoldPlaced.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactEngagementHoldPlaced domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactEngagementHoldPlaced.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactEngagementHoldReleased

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactEngagementHoldReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactEngagementHoldReleased |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-engagement-hold-released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactEngagementHoldReleased.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactEngagementHoldReleased domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactEngagementHoldReleased.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X007](../reactors/client-export-delivery-reactor.md#x007) | emitted semantic fact | ClientExport | RecordEngagementPackageDelivered | ArtifactEngagementHold |

### EngagementReportIssued

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EngagementReportIssued |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EngagementReportIssued |
| Adopted wire | `{"discriminator":"bdgrz.retention.engagement-report-issued","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/EngagementReportIssued.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the EngagementReportIssued domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/EngagementReportIssued.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
