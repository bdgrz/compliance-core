# AuditPopulationExportProcess commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/audit-population-export-process.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| GenerateAuditPopulationExport | GenerateAuditPopulationExportHandler | true | technical_process_request |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### GenerateAuditPopulationExport

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/AuditPopulations/Commands/GenerateAuditPopulationExport.cs](../../../src/Compliance.Common/Features/AuditPopulations/Commands/GenerateAuditPopulationExport.cs)
- Handler: [src/Compliance.Core/Features/AuditPopulations/Handlers/GenerateAuditPopulationExportHandler.cs](../../../src/Compliance.Core/Features/AuditPopulations/Handlers/GenerateAuditPopulationExportHandler.cs)
- Discriminator: `bdgrz.audit-populations.generate-audit-population-export@2`
- Request interfaces: IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable
- Response: `AuditPopulationExportReceipt`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| PopulationId | Uuid | true |
| Version | long | true |
| Format | string | true |
| ExportKey | string | true |
