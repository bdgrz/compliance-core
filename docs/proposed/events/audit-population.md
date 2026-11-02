# AuditPopulation events

Canonical events: **8**. Complete canonical event and available schema records: [owner event JSON](../data/events/audit-population.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditPopulationAmended | business | bdgrz.auditpopulations.audit-population-amended | 3 | [Placeholder: schema absent] | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| AuditPopulationDefined | business | bdgrz.auditpopulations.audit-population-defined | 4 | [Placeholder: schema absent] | Records a population definition with its original command and response receipt. |
| AuditPopulationDefinitionRevised | business | bdgrz.auditpopulations.audit-population-definition-revised | 2 | [Placeholder: schema absent] | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| AuditPopulationFactRecorded | business | bdgrz.auditpopulations.audit-population-fact-recorded | 3 | [Placeholder: schema absent] | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| AuditPopulationFrozen | business | bdgrz.auditpopulations.audit-population-frozen | 3 | [Placeholder: schema absent] | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| AuditPopulationGenerated | business | bdgrz.auditpopulations.audit-population-generated | 4 | [Placeholder: schema absent] | Records collection intent with its original command and response receipt. |
| AuditPopulationReconciled | business | bdgrz.auditpopulations.audit-population-reconciled | 3 | [Placeholder: schema absent] | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| AuditPopulationRowClassified | business | bdgrz.auditpopulations.audit-population-row-classified | 3 | [Placeholder: schema absent] | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |

## Event details

### AuditPopulationAmended

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationAmended |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationAmendedV3 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-amended","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationAmendedV3.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-amended","version":2}` |
| Meaning | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationAmendedV3.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationDefined

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationDefined |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationDefinedV4 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-defined","version":4}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationDefinedV4.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-defined","version":2}` |
| Meaning | Records a population definition with its original command and response receipt. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationDefinedV4.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationDefinitionRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationDefinitionRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationDefinitionRevisedV2 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-definition-revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationDefinitionRevisedV2.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-definition-revised","version":1}` |
| Meaning | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationDefinitionRevisedV2.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationFactRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationFactRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationFactRecordedV3 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-fact-recorded","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationFactRecordedV3.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-fact-recorded","version":2}` |
| Meaning | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationFactRecordedV3.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationFrozen

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationFrozen |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationFrozenV3 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-frozen","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationFrozenV3.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-frozen","version":2}` |
| Meaning | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationFrozenV3.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationGenerated

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationGenerated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationGeneratedV4 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-generated","version":4}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationGeneratedV4.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-generated","version":2}` |
| Meaning | Records collection intent with its original command and response receipt. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationGeneratedV4.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationReconciled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationReconciled |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationReconciledV3 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-reconciled","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationReconciledV3.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-reconciled","version":2}` |
| Meaning | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationReconciledV3.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditPopulationRowClassified

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditPopulationRowClassified |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditPopulationRowClassifiedV3 |
| Adopted wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-row-classified","version":3}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationRowClassifiedV3.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.auditpopulations.audit-population-row-classified","version":2}` |
| Meaning | Complete immutable local decision; occupied inline prior versions retain their exact unqualified tags. |
| Meaning source | record summary in src/Compliance.Common/Features/AuditPopulations/Events/AuditPopulationRowClassifiedV3.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
