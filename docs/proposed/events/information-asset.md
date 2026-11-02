# InformationAsset events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/information-asset.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| InformationAssetRevisionRecorded | business | bdgrz.inventory.information_asset.revision_recorded | 1 | [Placeholder: schema absent] | Records one complete, attributed asset revision; revision 1 creates it. The fact accepts its own logical operation, so an exact retry of the creation or revision replays the frozen original response however far the record has moved since. |

## Event details

### InformationAssetRevisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:InformationAssetRevisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | InformationAssetRevisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.inventory.information_asset.revision_recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/TechnologyInventory/Events/InformationAssetRevisionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records one complete, attributed asset revision; revision 1 creates it. The fact accepts its own logical operation, so an exact retry of the creation or revision replays the frozen original response however far the record has moved since. |
| Meaning source | record summary in src/Compliance.Common/Features/TechnologyInventory/Events/InformationAssetRevisionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
