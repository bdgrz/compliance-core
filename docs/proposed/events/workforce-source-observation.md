# WorkforceSourceObservation events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/workforce-source-observation.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| WorkforceSourceObserved | WorkforceSourceObserved | business | bdgrz.workforce.source.observed | 1 | [Placeholder: schema absent] | An HRIS, identity provider or provider system's record revision was observed against a target person, work relationship or service identity, capturing the source facts seen at that time. |
| WorkforceSourceReconciled | WorkforceSourceReconciled | business | bdgrz.workforce.source.reconciled | 1 | [Placeholder: schema absent] | A workforce source observation was reconciled by a named decision to accept or dismiss it with a note, settling any conflict with the canonical workforce record. |

## Event details

### WorkforceSourceObserved

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkforceSourceObserved |
| Selected source event name | WorkforceSourceObserved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WorkforceSourceObserved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkforceSourceObserved |
| Adopted wire | `{"discriminator":"bdgrz.workforce.source.observed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/WorkforceSourceObserved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An HRIS, identity provider or provider system's record revision was observed against a target person, work relationship or service identity, capturing the source facts seen at that time. |
| Selected source meaning | An HRIS, identity provider or provider system's record revision was observed against a target person, work relationship or service identity, capturing the source facts seen at that time. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/WorkforceSourceObserved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkforceSourceReconciled

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkforceSourceReconciled |
| Selected source event name | WorkforceSourceReconciled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WorkforceSourceReconciled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkforceSourceReconciled |
| Adopted wire | `{"discriminator":"bdgrz.workforce.source.reconciled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/WorkforceSourceReconciled.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A workforce source observation was reconciled by a named decision to accept or dismiss it with a note, settling any conflict with the canonical workforce record. |
| Selected source meaning | A workforce source observation was reconciled by a named decision to accept or dismiss it with a note, settling any conflict with the canonical workforce record. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/WorkforceSourceReconciled.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
