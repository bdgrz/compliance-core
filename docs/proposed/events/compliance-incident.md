# ComplianceIncident events

Canonical events: **5**. Complete canonical event and available schema records: [owner event JSON](../data/events/compliance-incident.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ComplianceIncidentImpactAssessed | ComplianceIncidentImpactAssessed | business | bdgrz.incidents.compliance-incident-impact-assessed | 3 | [Placeholder: schema absent] | The impact of a compliance incident was assessed by a named member, becoming the incident's current assessment. |
| ComplianceIncidentRecorded | ComplianceIncidentRecorded | business | bdgrz.incidents.compliance-incident-recorded | 3 | [Placeholder: schema absent] | A compliance incident was recorded for a program by a named member as the incident's first revision. |
| ComplianceIncidentRevised | ComplianceIncidentRevised | business | bdgrz.incidents.compliance-incident-revised | 3 | [Placeholder: schema absent] | A recorded compliance incident was revised by a named member, changing its content at the next revision. |
| ComplianceIncidentSystemDescriptionDecisionRecorded | ComplianceIncidentSystemDescriptionDecisionRecorded | business | bdgrz.incidents.compliance-incident-system-description-decision-recorded | 3 | [Placeholder: schema absent] | A decision was recorded on whether a compliance incident requires a change to the system description, made by a named member. |
| IncidentResolutionRecorded | IncidentResolutionRecorded | business | bdgrz.incidents.incident-resolution-recorded | 3 | [Placeholder: schema absent] | The resolution of a compliance incident was recorded by a named member, closing the incident. |

## Event details

### ComplianceIncidentImpactAssessed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ComplianceIncidentImpactAssessed |
| Selected source event name | ComplianceIncidentImpactAssessed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ComplianceIncidentImpactAssessed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ComplianceIncidentImpactAssessed |
| Adopted wire | `{"discriminator":"bdgrz.incidents.compliance-incident-impact-assessed","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentImpactAssessed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The impact of a compliance incident was assessed by a named member, becoming the incident's current assessment. |
| Selected source meaning | The impact of a compliance incident was assessed by a named member, becoming the incident's current assessment. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentImpactAssessed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ComplianceIncidentRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ComplianceIncidentRecorded |
| Selected source event name | ComplianceIncidentRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ComplianceIncidentRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ComplianceIncidentRecorded |
| Adopted wire | `{"discriminator":"bdgrz.incidents.compliance-incident-recorded","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A compliance incident was recorded for a program by a named member as the incident's first revision. |
| Selected source meaning | A compliance incident was recorded for a program by a named member as the incident's first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ComplianceIncidentRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | ComplianceIncidentRevised |
| Selected source event name | ComplianceIncidentRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ComplianceIncidentRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ComplianceIncidentRevised |
| Adopted wire | `{"discriminator":"bdgrz.incidents.compliance-incident-revised","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A recorded compliance incident was revised by a named member, changing its content at the next revision. |
| Selected source meaning | A recorded compliance incident was revised by a named member, changing its content at the next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ComplianceIncidentSystemDescriptionDecisionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | ComplianceIncidentSystemDescriptionDecisionRecorded |
| Selected source event name | ComplianceIncidentSystemDescriptionDecisionRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ComplianceIncidentSystemDescriptionDecisionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ComplianceIncidentSystemDescriptionDecisionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.incidents.compliance-incident-system-description-decision-recorded","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentSystemDescriptionDecisionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A decision was recorded on whether a compliance incident requires a change to the system description, made by a named member. |
| Selected source meaning | A decision was recorded on whether a compliance incident requires a change to the system description, made by a named member. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Incidents/Events/ComplianceIncidentSystemDescriptionDecisionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S021](../reactors/update-system-description-after-significant-change-reactor.md#s021) | emitted semantic fact | SystemDescription | RecordComplianceIncidentSystemDescriptionDecision | ComplianceIncident |

### IncidentResolutionRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | IncidentResolutionRecorded |
| Selected source event name | IncidentResolutionRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:IncidentResolutionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | IncidentResolutionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.incidents.incident-resolution-recorded","version":3}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Incidents/Events/IncidentResolutionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The resolution of a compliance incident was recorded by a named member, closing the incident. |
| Selected source meaning | The resolution of a compliance incident was recorded by a named member, closing the incident. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Incidents/Events/IncidentResolutionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
