# ArtifactDisposition events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/artifact-disposition.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ArtifactDispositionApproved | business | bdgrz.retention.artifact-disposition-approved | 1 | [Placeholder: schema absent] | Records the ArtifactDispositionApproved domain fact for one tenant-owned subject revision. |
| ArtifactDispositionExecutionRequested | business | bdgrz.retention.artifact-disposition-execution-requested | 1 | [Placeholder: schema absent] | Records the ArtifactDispositionExecutionRequested domain fact for one tenant-owned subject revision. |
| ArtifactDispositionResultRecorded | business | bdgrz.retention.artifact-disposition-result-recorded | 1 | [Placeholder: schema absent] | Records the ArtifactDispositionResultRecorded domain fact for one tenant-owned subject revision. |

## Event details

### ArtifactDispositionApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDispositionApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDispositionApproved |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-disposition-approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactDispositionApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactDispositionApproved domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactDispositionApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactDispositionExecutionRequested

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDispositionExecutionRequested |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDispositionExecutionRequested |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-disposition-execution-requested","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactDispositionExecutionRequested.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactDispositionExecutionRequested domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactDispositionExecutionRequested.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactDispositionResultRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDispositionResultRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDispositionResultRecorded |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-disposition-result-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactDispositionResultRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactDispositionResultRecorded domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactDispositionResultRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X006](../reactors/artifact-legal-hold-reactor.md#x006) | emitted semantic fact | ArtifactLegalHold | FenceArtifactDispositionForHold | ArtifactDisposition |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
