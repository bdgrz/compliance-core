# ProgramRollover events

Canonical events: **6**. Complete canonical event and available schema records: [owner event JSON](../data/events/program-rollover.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ProgramNextPeriodCreated | ProgramNextPeriodCreated | business | bdgrz.programs.program-next-period-created | 1 | [Placeholder: schema absent] | Records the exact next period and approved plan created from a rollover. |
| ProgramRolloverApproved | ProgramRolloverApproved | business | bdgrz.programs.program-rollover-approved | 2 | [Placeholder: schema absent] | Records the ProgramRolloverApproved domain fact for one tenant-owned subject revision. |
| ProgramRolloverDecisionRecorded | ProgramRolloverDecisionRecorded | business | bdgrz.programs.program-rollover-decision-recorded | 1 | [Placeholder: schema absent] | A decision on a program carry-forward proposal version was recorded by someone other than its proposer, rejecting that proposal version. |
| ProgramRolloverReservedForExecution | ProgramRolloverExecutionClaimed | business | bdgrz.programs.program-rollover-execution-claimed | 1 | [Placeholder: schema absent] | The exact independently approved rollover version and content were reserved for one run. The reservation prevents revision or rejection while the successor period is prepared. |
| ProgramRolloverProposed | ProgramRolloverProposed | business | bdgrz.programs.program-rollover-proposed | 2 | [Placeholder: schema absent] | Records the ProgramRolloverProposed domain fact for one tenant-owned subject revision. |
| ProgramRolloverRevised | ProgramRolloverRevised | business | bdgrz.programs.program-rollover-revised | 1 | [Placeholder: schema absent] | Records a revised carry-forward selection against the same completed source period. |

## Event details

### ProgramNextPeriodCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramNextPeriodCreated |
| Selected source event name | ProgramNextPeriodCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramNextPeriodCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramNextPeriodCreated |
| Adopted wire | `{"discriminator":"bdgrz.programs.program-next-period-created","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramNextPeriodCreated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the exact next period and approved plan created from a rollover. |
| Selected source meaning | Records the exact next period and approved plan created from a rollover. |
| Meaning source | record summary in src/Compliance.Common/Features/Programs/Events/ProgramNextPeriodCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProgramRolloverApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramRolloverApproved |
| Selected source event name | ProgramRolloverApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramRolloverApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramRolloverApproved |
| Adopted wire | `{"discriminator":"bdgrz.programs.program-rollover-approved","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramRolloverApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ProgramRolloverApproved domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ProgramRolloverApproved domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Programs/Events/ProgramRolloverApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [R000](../reactors/one-new-committed-execution-claim-starter.md#r000) | logical prerequisite | ProgramRollover | StartProgramRolloverExecutionProcess | ProgramRolloverExecutionProcess |

### ProgramRolloverDecisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramRolloverDecisionRecorded |
| Selected source event name | ProgramRolloverDecisionRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramRolloverDecisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramRolloverDecisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.programs.program-rollover-decision-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramRolloverDecisionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A decision on a program carry-forward proposal version was recorded by someone other than its proposer, rejecting that proposal version. |
| Selected source meaning | A decision on a program carry-forward proposal version was recorded by someone other than its proposer, rejecting that proposal version. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ProgramRolloverDecisionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProgramRolloverReservedForExecution

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramRolloverReservedForExecution |
| Selected source event name | ProgramRolloverExecutionClaimed |
| Name decision | Names the reservation of the exact approved rollover for one run without exposing the internal claim mechanism. |
| Canonical semantic key | event:ProgramRolloverExecutionClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramRolloverExecutionClaimed |
| Adopted wire | `{"discriminator":"bdgrz.programs.program-rollover-execution-claimed","version":1}` |
| Wire qualification | New target schema; discriminator collision check required before implementation. |
| Schema status | NEW_SELECTED_TARGET_NO_IMPLEMENTATION_AUTHORIZED |
| Source status | planned_no_source_type |
| Adopted decision IDs | PROGRAM_ROLLOVER_EXECUTION |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The exact independently approved rollover version and content were reserved for one run. The reservation prevents revision or rejection while the successor period is prepared. |
| Selected source meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [R000](../reactors/one-new-committed-execution-claim-starter.md#r000) | physical root | ProgramRollover | StartProgramRolloverExecutionProcess | ProgramRolloverExecutionProcess |

### ProgramRolloverProposed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramRolloverProposed |
| Selected source event name | ProgramRolloverProposed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramRolloverProposed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramRolloverProposed |
| Adopted wire | `{"discriminator":"bdgrz.programs.program-rollover-proposed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramRolloverProposed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the ProgramRolloverProposed domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the ProgramRolloverProposed domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Programs/Events/ProgramRolloverProposed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ProgramRolloverRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | ProgramRolloverRevised |
| Selected source event name | ProgramRolloverRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ProgramRolloverRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ProgramRolloverRevised |
| Adopted wire | `{"discriminator":"bdgrz.programs.program-rollover-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Programs/Events/ProgramRolloverRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records a revised carry-forward selection against the same completed source period. |
| Selected source meaning | Records a revised carry-forward selection against the same completed source period. |
| Meaning source | record summary in src/Compliance.Common/Features/Programs/Events/ProgramRolloverRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
