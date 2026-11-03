# ComplianceProgram events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/compliance-program.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ProgramCreated | ProgramCreated | business | bdgrz.program.created | 2 | [Placeholder: schema absent] | A compliance program was created with its authored content, recorded as the program's first revision. |
| ProgramCriteriaEditionSelected | ProgramCriteriaEditionSelected | business | bdgrz.program.criteria.selected | 2 | [Placeholder: schema absent] | A criteria edition was selected for a compliance program, becoming the edition its criteria are measured against; re-selecting the same edition leaves the revision unchanged. |
| ProgramRevised | ProgramRevised | business | bdgrz.program.revised | 2 | [Placeholder: schema absent] | A compliance program's authored content was revised, recorded as the program's next revision. |

## Event details

### ProgramCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramCreated |
| Selected source event name | ProgramCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramCreated |
| Adopted wire | `{"discriminator":"bdgrz.program.created","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A compliance program was created with its authored content, recorded as the program's first revision. |
| Selected source meaning | A compliance program was created with its authored content, recorded as the program's first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ProgramCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProgramCriteriaEditionSelected

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramCriteriaEditionSelected |
| Selected source event name | ProgramCriteriaEditionSelected |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramCriteriaEditionSelected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramCriteriaEditionSelected |
| Adopted wire | `{"discriminator":"bdgrz.program.criteria.selected","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramCriteriaEditionSelected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A criteria edition was selected for a compliance program, becoming the edition its criteria are measured against; re-selecting the same edition leaves the revision unchanged. |
| Selected source meaning | A criteria edition was selected for a compliance program, becoming the edition its criteria are measured against; re-selecting the same edition leaves the revision unchanged. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ProgramCriteriaEditionSelected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProgramRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramRevised |
| Selected source event name | ProgramRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramRevised |
| Adopted wire | `{"discriminator":"bdgrz.program.revised","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A compliance program's authored content was revised, recorded as the program's next revision. |
| Selected source meaning | A compliance program's authored content was revised, recorded as the program's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ProgramRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
