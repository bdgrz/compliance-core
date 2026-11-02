# PopulationSnapshot events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/population-snapshot.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| PopulationSnapshotFrozen | business | bdgrz.snapshot.population.frozen | 1 | [Placeholder: schema absent] | An immutable population snapshot: canonical, key-ordered rows whose content identity is the domain-separated population SHA-256. Amendments are new snapshots linked to the original. A population that fits one storage chunk keeps its rows inline. A larger population keeps no rows here: StoredChunkCount chunk events hold them, and StorageManifestSha256 binds those chunks in order. |

## Event details

### PopulationSnapshotFrozen

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:PopulationSnapshotFrozen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PopulationSnapshotFrozen |
| Adopted wire | `{"discriminator":"bdgrz.snapshot.population.frozen","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Snapshots/Events/PopulationSnapshotFrozen.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An immutable population snapshot: canonical, key-ordered rows whose content identity is the domain-separated population SHA-256. Amendments are new snapshots linked to the original. A population that fits one storage chunk keeps its rows inline. A larger population keeps no rows here: StoredChunkCount chunk events hold them, and StorageManifestSha256 binds those chunks in order. |
| Meaning source | record summary in src/Compliance.Common/Features/Snapshots/Events/PopulationSnapshotFrozen.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
