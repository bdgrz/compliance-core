# AuditPopulation

- Classification: `business_aggregate`
- Source area: `AuditPopulations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / audit-populations / PopulationId, where PopulationId equals the caller-supplied DefinitionId of the defining command (AuditPopulationMutations.DefineAsync constructs the owner with DefinitionId as the population identity); an exact retry of DefineAuditPopulation with the same DefinitionId resolves the original result, and later commands carry PopulationId explicitly with CommandId or GenerationKey and the expected revision.

**Invariant:** One population owns complete current definition/source/row classification, reconciliation invalidation, completeness proof and freeze seal. Immutable row/version bodies are values outside retained command state; no independent AuditPopulationRowDecision root or projected-count freeze authority.

**Additional decision rule:** A manually recorded population cannot claim audit-ready or freeze until its scope, observation time and completeness are declared. Late or corrected rows never edit a frozen version: they produce a new population version that references the superseded version and each superseded row.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AmendAuditPopulation](../commands/audit-population.md#amendauditpopulation) | command | CommandId |
| [ClassifyAuditPopulationRow](../commands/audit-population.md#classifyauditpopulationrow) | command | CommandId |
| [DefineAuditPopulation](../commands/audit-population.md#defineauditpopulation) | creation | DefinitionId |
| [FreezeAuditPopulation](../commands/audit-population.md#freezeauditpopulation) | command | CommandId |
| [GenerateAuditPopulation](../commands/audit-population.md#generateauditpopulation) | creation | GenerationKey |
| [ReconcileAuditPopulation](../commands/audit-population.md#reconcileauditpopulation) | command | CommandId |
| [RecordAuditPopulationFacts](../commands/audit-population.md#recordauditpopulationfacts) | command | CommandId |
| [ReviseAuditPopulationDefinition](../commands/audit-population.md#reviseauditpopulationdefinition) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AuditPopulationAmended](../events/audit-population.md#auditpopulationamended) | bdgrz.auditpopulations.audit-population-amended | 3 |
| [AuditPopulationDefined](../events/audit-population.md#auditpopulationdefined) | bdgrz.auditpopulations.audit-population-defined | 4 |
| [AuditPopulationDefinitionRevised](../events/audit-population.md#auditpopulationdefinitionrevised) | bdgrz.auditpopulations.audit-population-definition-revised | 2 |
| [AuditPopulationFactRecorded](../events/audit-population.md#auditpopulationfactrecorded) | bdgrz.auditpopulations.audit-population-fact-recorded | 3 |
| [AuditPopulationFrozen](../events/audit-population.md#auditpopulationfrozen) | bdgrz.auditpopulations.audit-population-frozen | 3 |
| [AuditPopulationGenerated](../events/audit-population.md#auditpopulationgenerated) | bdgrz.auditpopulations.audit-population-generated | 4 |
| [AuditPopulationReconciled](../events/audit-population.md#auditpopulationreconciled) | bdgrz.auditpopulations.audit-population-reconciled | 3 |
| [AuditPopulationRowClassified](../events/audit-population.md#auditpopulationrowclassified) | bdgrz.auditpopulations.audit-population-row-classified | 3 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D07-12** — A team must declare scope, observation time, and completeness of a manually recorded population before it can claim an audit-ready population. Decision: [m0-d07-access-review-population.md](../decisions/m0-d07-access-review-population.md).
- **D17-02** — Late or corrected population rows produce a new population version that references the superseded version and each superseded row. Decision: [m0-d17-audit-deliverables.md](../decisions/m0-d17-audit-deliverables.md).

## Adopted architecture decisions

- `OWNER_AuditPopulationRowDecision` — parent_owns_current_reconciliation.

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/audit-population.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
