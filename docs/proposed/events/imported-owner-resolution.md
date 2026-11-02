# ImportedOwnerResolution events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/imported-owner-resolution.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ImportedOwnerResolutionRecorded | business | null (recorded) | null (recorded) | [Placeholder: schema absent] | An imported owner reference was bound to a workforce person by exact email match at import or by one attributed manual resolution; unresolved references stay unresolved until then. |

## Event details

### ImportedOwnerResolutionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ImportedOwnerResolutionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ImportedOwnerResolutionRecorded |
| Adopted wire | null (recorded) |
| Wire qualification | Planned; no declared record yet. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | OWNER_ImportedOwnerResolution |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An imported owner reference was bound to a workforce person by exact email match at import or by one attributed manual resolution; unresolved references stay unresolved until then. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
