# TypeIIPlan events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/type-ii-plan.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TypeIIPlanApproved | TypeIIPlanApproved | business | bdgrz.examinations.type-i-i-plan-approved | 2 | [Placeholder: schema absent] | Records independent executive approval of the reviewed Type II plan version. |
| TypeIIPlanProposed | TypeIIPlanProposed | business | bdgrz.examinations.type-i-i-plan-proposed | 2 | [Placeholder: schema absent] | Creates a versioned Type II operating plan with exact source and control bindings. |
| TypeIIPlanReviewed | TypeIIPlanReviewed | business | bdgrz.examinations.type-i-i-plan-reviewed | 1 | [Placeholder: schema absent] | Records the independent review of the current Type II plan version. |
| TypeIIPlanRevised | TypeIIPlanRevised | business | bdgrz.examinations.type-i-i-plan-revised | 1 | [Placeholder: schema absent] | Appends a revised immutable Type II plan after a review requests changes. |

## Event details

### TypeIIPlanApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | TypeIIPlanApproved |
| Selected source event name | TypeIIPlanApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TypeIIPlanApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPlanApproved |
| Adopted wire | `{"discriminator":"bdgrz.examinations.type-i-i-plan-approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records independent executive approval of the reviewed Type II plan version. |
| Selected source meaning | Records independent executive approval of the reviewed Type II plan version. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIIPlanProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | TypeIIPlanProposed |
| Selected source event name | TypeIIPlanProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TypeIIPlanProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPlanProposed |
| Adopted wire | `{"discriminator":"bdgrz.examinations.type-i-i-plan-proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Creates a versioned Type II operating plan with exact source and control bindings. |
| Selected source meaning | Creates a versioned Type II operating plan with exact source and control bindings. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIIPlanReviewed

| Canonical event detail | Value |
| --- | --- |
| Target event name | TypeIIPlanReviewed |
| Selected source event name | TypeIIPlanReviewed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TypeIIPlanReviewed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPlanReviewed |
| Adopted wire | `{"discriminator":"bdgrz.examinations.type-i-i-plan-reviewed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanReviewed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the independent review of the current Type II plan version. |
| Selected source meaning | Records the independent review of the current Type II plan version. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanReviewed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TypeIIPlanRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | TypeIIPlanRevised |
| Selected source event name | TypeIIPlanRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TypeIIPlanRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TypeIIPlanRevised |
| Adopted wire | `{"discriminator":"bdgrz.examinations.type-i-i-plan-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Appends a revised immutable Type II plan after a review requests changes. |
| Selected source meaning | Appends a revised immutable Type II plan after a review requests changes. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/TypeIIPlanRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
