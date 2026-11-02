# WorkDigestPreference events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/work-digest-preference.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| WorkDigestPreferenceRecorded | technical | bdgrz.work.digest_preference.recorded | 1 | [Placeholder: schema absent] | A member's authored preference receipt, including an accepted no-change decision. |

## Event details

### WorkDigestPreferenceRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkDigestPreferenceRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkDigestPreferenceRecorded |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_preference.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestPreferenceRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | EV_D05 |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A member's authored preference receipt, including an accepted no-change decision. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WorkDigestPreferenceRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
