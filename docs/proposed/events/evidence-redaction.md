# EvidenceRedaction events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/evidence-redaction.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| EvidenceRedactionApprovedV2 | business | bdgrz.evidence.redaction.approved | 2 | [Placeholder: schema absent] | Native personal approval of an exact preparation and observed available derivative. |
| EvidenceRedactionPreparedV2 | business | bdgrz.evidence.redaction.prepared | 2 | [Placeholder: schema absent] | Native preparation with exact source qualification and frozen original result; occupied inline v1 remains legacy. |

## Event details

### EvidenceRedactionApprovedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRedactionApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRedactionApprovedV2 |
| Adopted wire | `{"discriminator":"bdgrz.evidence.redaction.approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceRedactionApprovedV2.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Native personal approval of an exact preparation and observed available derivative. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRedactionApprovedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### EvidenceRedactionPreparedV2

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:EvidenceRedactionPrepared |
| Owner selection | one exact canonical authority owner |
| Target concrete type | EvidenceRedactionPreparedV2 |
| Adopted wire | `{"discriminator":"bdgrz.evidence.redaction.prepared","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Evidence/Events/EvidenceRedactionPreparedV2.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Native preparation with exact source qualification and frozen original result; occupied inline v1 remains legacy. |
| Meaning source | record summary in src/Compliance.Common/Features/Evidence/Events/EvidenceRedactionPreparedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
