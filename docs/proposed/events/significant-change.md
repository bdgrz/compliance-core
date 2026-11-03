# SignificantChange events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/significant-change.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ImpactReviewWorkLinked | ImpactReviewWorkLinked | business | bdgrz.changes.impact-review-work-linked | 3 | [Placeholder: schema absent] | Review work was linked to a significant change to carry out the follow-up its impact requires, recorded by a named member. |
| SignificantChangeImpactAssessed | SignificantChangeImpactAssessed | business | bdgrz.changes.significant-change-impact-assessed | 3 | [Placeholder: schema absent] | The impact of a significant change on the system and its controls was assessed by a named member, becoming the change's current assessment. |
| SignificantChangeRecorded | SignificantChangeRecorded | business | bdgrz.changes.significant-change-recorded | 3 | [Placeholder: schema absent] | A significant change to the system was recorded for a program by a named member as the change's first revision. |
| SignificantChangeRevised | SignificantChangeRevised | business | bdgrz.changes.significant-change-revised | 3 | [Placeholder: schema absent] | A recorded significant change was revised by a named member, changing its content at the next revision. |
| SignificantChangeSystemDescriptionDecisionRecorded | SignificantChangeSystemDescriptionDecisionRecorded | business | bdgrz.changes.significant-change-system-description-decision-recorded | 2 | [Placeholder: schema absent] | A decision was recorded on whether a significant change requires an update to the system description, made by a named member. |

## Event details

### ImpactReviewWorkLinked

| Canonical event detail | Value |
| --- | --- |
| Target event name | ImpactReviewWorkLinked |
| Selected source event name | ImpactReviewWorkLinked |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ImpactReviewWorkLinked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ImpactReviewWorkLinked |
| Adopted wire | `{"discriminator":"bdgrz.changes.impact-review-work-linked","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Changes/Events/ImpactReviewWorkLinked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Review work was linked to a significant change to carry out the follow-up its impact requires, recorded by a named member. |
| Selected source meaning | Review work was linked to a significant change to carry out the follow-up its impact requires, recorded by a named member. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Changes/Events/ImpactReviewWorkLinked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S019](../reactors/link-impact-review-work-reactor.md#s019) | emitted semantic fact | SignificantChange | LinkImpactReviewWork | SignificantChange |

### SignificantChangeImpactAssessed

| Canonical event detail | Value |
| --- | --- |
| Target event name | SignificantChangeImpactAssessed |
| Selected source event name | SignificantChangeImpactAssessed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SignificantChangeImpactAssessed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SignificantChangeImpactAssessed |
| Adopted wire | `{"discriminator":"bdgrz.changes.significant-change-impact-assessed","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Changes/Events/SignificantChangeImpactAssessed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The impact of a significant change on the system and its controls was assessed by a named member, becoming the change's current assessment. |
| Selected source meaning | The impact of a significant change on the system and its controls was assessed by a named member, becoming the change's current assessment. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Changes/Events/SignificantChangeImpactAssessed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S019](../reactors/link-impact-review-work-reactor.md#s019) | physical root, logical prerequisite | SignificantChange | LinkImpactReviewWork | SignificantChange |

### SignificantChangeRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | SignificantChangeRecorded |
| Selected source event name | SignificantChangeRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SignificantChangeRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SignificantChangeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.changes.significant-change-recorded","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Changes/Events/SignificantChangeRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A significant change to the system was recorded for a program by a named member as the change's first revision. |
| Selected source meaning | A significant change to the system was recorded for a program by a named member as the change's first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Changes/Events/SignificantChangeRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SignificantChangeRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | SignificantChangeRevised |
| Selected source event name | SignificantChangeRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SignificantChangeRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SignificantChangeRevised |
| Adopted wire | `{"discriminator":"bdgrz.changes.significant-change-revised","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Changes/Events/SignificantChangeRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A recorded significant change was revised by a named member, changing its content at the next revision. |
| Selected source meaning | A recorded significant change was revised by a named member, changing its content at the next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Changes/Events/SignificantChangeRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SignificantChangeSystemDescriptionDecisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | SignificantChangeSystemDescriptionDecisionRecorded |
| Selected source event name | SignificantChangeSystemDescriptionDecisionRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SignificantChangeSystemDescriptionDecisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SignificantChangeSystemDescriptionDecisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.changes.significant-change-system-description-decision-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Changes/Events/SignificantChangeSystemDescriptionDecisionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A decision was recorded on whether a significant change requires an update to the system description, made by a named member. |
| Selected source meaning | A decision was recorded on whether a significant change requires an update to the system description, made by a named member. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Changes/Events/SignificantChangeSystemDescriptionDecisionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S020](../reactors/system-description-decision-reactor.md#s020) | emitted semantic fact | SystemDescription | RecordSignificantChangeSystemDescriptionDecision | SignificantChange |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
