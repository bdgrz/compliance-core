# ArtifactDisclosure events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/artifact-disclosure.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ArtifactDisclosureApproved | business | bdgrz.retention.artifact-disclosure-approved | 1 | [Placeholder: schema absent] | Records the ArtifactDisclosureApproved domain fact for one tenant-owned subject revision. |
| ArtifactDisclosureDelivered | business | bdgrz.retention.artifact-disclosure-delivered | 1 | [Placeholder: schema absent] | Records the ArtifactDisclosureDelivered domain fact for one tenant-owned subject revision. |
| ArtifactDisclosureRevoked | business | bdgrz.retention.artifact-disclosure-revoked | 1 | [Placeholder: schema absent] | Records the ArtifactDisclosureRevoked domain fact for one tenant-owned subject revision. |

## Event details

### ArtifactDisclosureApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDisclosureApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDisclosureApproved |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-disclosure-approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactDisclosureApproved domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactDisclosureDelivered

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDisclosureDelivered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDisclosureDelivered |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-disclosure-delivered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureDelivered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactDisclosureDelivered domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureDelivered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ArtifactDisclosureRevoked

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ArtifactDisclosureRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ArtifactDisclosureRevoked |
| Adopted wire | `{"discriminator":"bdgrz.retention.artifact-disclosure-revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureRevoked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the ArtifactDisclosureRevoked domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Retention/Events/ArtifactDisclosureRevoked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
