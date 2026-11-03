# PopulationSnapshotChunk events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/population-snapshot-chunk.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| PopulationSnapshotPartRecorded | PopulationSnapshotChunkStored | technical | bdgrz.snapshot.population.chunk_stored | 1 | [Placeholder: schema absent] | One ordered storage chunk of a population snapshot's rows. Chunks are written before the snapshot's frozen manifest, so an interrupted freeze leaves no visible snapshot. ChunkSha256 is the population digest of this chunk's rows alone. |

## Event details

### PopulationSnapshotPartRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | PopulationSnapshotPartRecorded |
| Selected source event name | PopulationSnapshotChunkStored |
| Name decision | Names one ordered part of population data recorded before the complete snapshot is finalized. |
| Canonical semantic key | event:PopulationSnapshotChunkStored |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | PopulationSnapshotChunkStored |
| Adopted wire | `{"discriminator":"bdgrz.snapshot.population.chunk_stored","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Snapshots/Events/PopulationSnapshotChunkStored.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | One ordered storage chunk of a population snapshot's rows. Chunks are written before the snapshot's frozen manifest, so an interrupted freeze leaves no visible snapshot. ChunkSha256 is the population digest of this chunk's rows alone. |
| Selected source meaning | One ordered storage chunk of a population snapshot's rows. Chunks are written before the snapshot's frozen manifest, so an interrupted freeze leaves no visible snapshot. ChunkSha256 is the population digest of this chunk's rows alone. |
| Meaning source | record summary in src/Compliance.Common/Features/Snapshots/Events/PopulationSnapshotChunkStored.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
