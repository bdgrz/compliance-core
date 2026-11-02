# ImmutableSnapshot events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/immutable-snapshot.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| SnapshotFrozen | business | bdgrz.snapshot.frozen | 1 | [Placeholder: schema absent] | A program snapshot of a stated kind was frozen with its complete source manifest and content hash, optionally amending an earlier snapshot, and can no longer change. |

## Event details

### SnapshotFrozen

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:SnapshotFrozen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SnapshotFrozen |
| Adopted wire | `{"discriminator":"bdgrz.snapshot.frozen","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Snapshots/Events/SnapshotFrozen.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A program snapshot of a stated kind was frozen with its complete source manifest and content hash, optionally amending an earlier snapshot, and can no longer change. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Snapshots/Events/SnapshotFrozen.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
