# SystemInstanceAccessReviewScope events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/system-instance-access-review-scope.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AccessReviewScopeDecided | business | bdgrz.system_instance.access_review_scope.decided | 2 | [Placeholder: schema absent] | An attributable immutable personal scope decision at the exact named instance revision. |

## Event details

### AccessReviewScopeDecided

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AccessReviewScopeDecided |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewScopeDecided |
| Adopted wire | `{"discriminator":"bdgrz.system_instance.access_review_scope.decided","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/AccessReviewScopeDecided.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | An attributable immutable personal scope decision at the exact named instance revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/AccessReviewScopeDecided.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
