# ComplianceIncident

- Classification: `business_aggregate`
- Source area: `Incidents` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / compliance-incidents / IncidentId, with immutable ProgramId. IncidentId is the selected immutable creation identity; later selected commands use CommandId and the exact incident/version/proof bindings.

**Invariant:** One incident version may be assessed once; current-version independent description decision and required follow-up eligibility gate advancement; incident resolution requires every admitted follow-up work item completed.

**Additional decision rule:** An incident carries detected_at, resolved_at and boolean customer_data_affected and availability_sla_affected; the recorded time cannot precede detected_at and a revision cannot change detected_at. It may link to an earlier externally sourced IncidentReference; linking never rewrites earlier risk history and the reference keeps its source identity, occurrence time, summary and provenance.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssessComplianceIncident](../commands/compliance-incident.md#assesscomplianceincident) | command | CommandId |
| [RecordComplianceIncident](../commands/compliance-incident.md#recordcomplianceincident) | creation | IncidentId |
| [RecordComplianceIncidentResolution](../commands/compliance-incident.md#recordcomplianceincidentresolution) | command | CommandId |
| [RecordComplianceIncidentSystemDescriptionDecision](../commands/compliance-incident.md#recordcomplianceincidentsystemdescriptiondecision) | command | CommandId |
| [ReviseComplianceIncident](../commands/compliance-incident.md#revisecomplianceincident) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ComplianceIncidentImpactAssessed](../events/compliance-incident.md#complianceincidentimpactassessed) | bdgrz.incidents.compliance-incident-impact-assessed | 3 |
| [ComplianceIncidentRecorded](../events/compliance-incident.md#complianceincidentrecorded) | bdgrz.incidents.compliance-incident-recorded | 3 |
| [ComplianceIncidentRevised](../events/compliance-incident.md#complianceincidentrevised) | bdgrz.incidents.compliance-incident-revised | 3 |
| [ComplianceIncidentSystemDescriptionDecisionRecorded](../events/compliance-incident.md#complianceincidentsystemdescriptiondecisionrecorded) | bdgrz.incidents.compliance-incident-system-description-decision-recorded | 3 |
| [IncidentResolutionRecorded](../events/compliance-incident.md#incidentresolutionrecorded) | bdgrz.incidents.incident-resolution-recorded | 3 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S021](../reactors/update-system-description-after-significant-change-reactor.md#s021) | business_receiver | SystemDescription | ComplianceIncident | RecordComplianceIncidentSystemDescriptionDecision |

## Product rules

- **D21-04** — A ComplianceIncident carries detected_at, resolved_at, and boolean customer_data_affected and availability_sla_affected; it may link to an earlier externally sourced IncidentReference, and linking does not rewrite earlier risk history or weaken the IncidentReference's required source identity, occurrence time, summary, and provenance. Decision: [m0-d21-significant-change.md](../decisions/m0-d21-significant-change.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/compliance-incident.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
