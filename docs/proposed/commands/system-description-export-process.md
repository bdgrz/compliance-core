# SystemDescriptionExportProcess commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/system-description-export-process.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

| Selected source command | Handler | Registered | Entry point |
| --- | --- | --- | --- |
| GenerateSystemDescriptionExport | GenerateSystemDescriptionExportHandler | true | technical_process_request |

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### GenerateSystemDescriptionExport

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/SystemDescription/Commands/GenerateSystemDescriptionExport.cs](../../../src/Compliance.Common/Features/SystemDescription/Commands/GenerateSystemDescriptionExport.cs)
- Handler: [src/Compliance.Core/Features/SystemDescription/Handlers/GenerateSystemDescriptionExportHandler.cs](../../../src/Compliance.Core/Features/SystemDescription/Handlers/GenerateSystemDescriptionExportHandler.cs)
- Discriminator: `bdgrz.system-description.generate-system-description-export@2`
- Request interfaces: IRequest, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
- Response: `Unit`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| ProgramId | Uuid | true |
| DescriptionId | Uuid | true |
| Version | long | true |
| Format | string | true |
| ExportKey | string | true |
