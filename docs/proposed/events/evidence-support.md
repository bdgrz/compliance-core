# EvidenceSupport events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/evidence-support.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| EvidenceSupportLinked | business | bdgrz.evidence.evidence-support-linked | 1 | [Placeholder: schema absent] | Records the EvidenceSupportLinked domain fact for one tenant-owned subject revision. |

## Event details

### EvidenceSupportLinked

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceSupportLinked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceSupportLinked |
| Adopted wire | `{"discriminator":"bdgrz.evidence.evidence-support-linked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceSupportLinked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records the EvidenceSupportLinked domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceSupportLinked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
