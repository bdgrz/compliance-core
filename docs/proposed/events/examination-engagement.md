# ExaminationEngagement events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/examination-engagement.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ExaminationEngagementOpened | ExaminationEngagementOpened | business | bdgrz.examinations.examination-engagement-opened | 2 | [Placeholder: schema absent] | An examination engagement was opened for a program with its engagement type, system boundary and optional Type I baseline, as the engagement's first revision. |
| ExaminationEngagementRevised | ExaminationEngagementRevised | business | bdgrz.examinations.examination-engagement-revised | 2 | [Placeholder: schema absent] | An examination engagement's terms were revised by a named member, recorded as the engagement's next revision. |
| TypeIBaselineAmended | TypeIBaselineAmended | business | bdgrz.examinations.type-i-baseline-amended | 2 | [Placeholder: schema absent] | The frozen Type I baseline of an examination engagement was amended by a named member, recorded as a new baseline revision. |
| TypeIBaselineFrozen | TypeIBaselineFrozen | business | bdgrz.examinations.type-i-baseline-frozen | 2 | [Placeholder: schema absent] | The Type I baseline of an examination engagement was frozen at its current boundary, fixing the point-in-time position the engagement is measured against. |

## Event details

### ExaminationEngagementOpened

| Canonical event detail | Value |
| --- | --- |
| Target event name | ExaminationEngagementOpened |
| Selected source event name | ExaminationEngagementOpened |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ExaminationEngagementOpened |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ExaminationEngagementOpened |
| Adopted wire | `{"discriminator":"bdgrz.examinations.examination-engagement-opened","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ExaminationEngagementOpened.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An examination engagement was opened for a program with its engagement type, system boundary and optional Type I baseline, as the engagement's first revision. |
| Selected source meaning | An examination engagement was opened for a program with its engagement type, system boundary and optional Type I baseline, as the engagement's first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Examinations/Events/ExaminationEngagementOpened.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ExaminationEngagementRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | ExaminationEngagementRevised |
| Selected source event name | ExaminationEngagementRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ExaminationEngagementRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ExaminationEngagementRevised |
| Adopted wire | `{"discriminator":"bdgrz.examinations.examination-engagement-revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ExaminationEngagementRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An examination engagement's terms were revised by a named member, recorded as the engagement's next revision. |
| Selected source meaning | An examination engagement's terms were revised by a named member, recorded as the engagement's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Examinations/Events/ExaminationEngagementRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIBaselineAmended

| Canonical event detail | Value |
| --- | --- |
| Target event name | TypeIBaselineAmended |
| Selected source event name | TypeIBaselineAmended |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TypeIBaselineAmended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIBaselineAmended |
| Adopted wire | `{"discriminator":"bdgrz.examinations.type-i-baseline-amended","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/TypeIBaselineAmended.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The frozen Type I baseline of an examination engagement was amended by a named member, recorded as a new baseline revision. |
| Selected source meaning | The frozen Type I baseline of an examination engagement was amended by a named member, recorded as a new baseline revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Examinations/Events/TypeIBaselineAmended.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIBaselineFrozen

| Canonical event detail | Value |
| --- | --- |
| Target event name | TypeIBaselineFrozen |
| Selected source event name | TypeIBaselineFrozen |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TypeIBaselineFrozen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIBaselineFrozen |
| Adopted wire | `{"discriminator":"bdgrz.examinations.type-i-baseline-frozen","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/TypeIBaselineFrozen.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The Type I baseline of an examination engagement was frozen at its current boundary, fixing the point-in-time position the engagement is measured against. |
| Selected source meaning | The Type I baseline of an examination engagement was frozen at its current boundary, fixing the point-in-time position the engagement is measured against. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Examinations/Events/TypeIBaselineFrozen.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
