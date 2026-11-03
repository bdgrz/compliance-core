# SystemInstanceAccessReviewScope events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/system-instance-access-review-scope.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| AccessReviewScopeDecided | AccessReviewScopeDecided | business | bdgrz.system_instance.access_review_scope.decided | 2 | [Placeholder: schema absent] | An attributable immutable personal scope decision at the exact named instance revision. |

## Event details

### AccessReviewScopeDecided

| Canonical event detail | Value |
| --- | --- |
| Target event name | AccessReviewScopeDecided |
| Selected source event name | AccessReviewScopeDecided |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:AccessReviewScopeDecided |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AccessReviewScopeDecided |
| Adopted wire | `{"discriminator":"bdgrz.system_instance.access_review_scope.decided","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/AccessReviewScopeDecided.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An attributable immutable personal scope decision at the exact named instance revision. |
| Selected source meaning | An attributable immutable personal scope decision at the exact named instance revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/AccessReviewScopeDecided.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
