# Responsibility write commands

These two current request types route by record type to one of three existing business owners: ControlDraft, CommitmentDraft, or SystemBoundary. They share a request contract but do not introduce a separate responsibility aggregate. The selected target owner remains the scoped record.

## AssignResponsibility

- Source: [src/Compliance.Common/Features/Responsibilities/Commands/AssignResponsibility.cs](../../../src/Compliance.Common/Features/Responsibilities/Commands/AssignResponsibility.cs)
- Handler: [src/Compliance.Core/Features/Responsibilities/Handlers/AssignResponsibilityHandler.cs](../../../src/Compliance.Core/Features/Responsibilities/Handlers/AssignResponsibilityHandler.cs)
- Discriminator: `bdgrz.responsibility.assign@2`
- Request interfaces: IRequest, IRbacManagementRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
- Response: `Unit`

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| MemberUserId | Uuid | true |
| Type | ResponsibilityType | true |
| RecordType | string | true |
| RecordId | Uuid | true |
| VersionId | Uuid | true |
| ScopeRevision | long | true |
| EffectiveFrom | DateTimeOffset | true |
| EffectiveUntil | DateTimeOffset? | true |
| SeparationOfDutiesWaiverIds | IReadOnlyList<Uuid> | true |

## RevokeResponsibility

- Source: [src/Compliance.Common/Features/Responsibilities/Commands/RevokeResponsibility.cs](../../../src/Compliance.Common/Features/Responsibilities/Commands/RevokeResponsibility.cs)
- Handler: [src/Compliance.Core/Features/Responsibilities/Handlers/RevokeResponsibilityHandler.cs](../../../src/Compliance.Core/Features/Responsibilities/Handlers/RevokeResponsibilityHandler.cs)
- Discriminator: `bdgrz.responsibility.revoke@2`
- Request interfaces: IRequest, IRbacManagementRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
- Response: `Unit`

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| RecordType | string | true |
| RecordId | Uuid | true |
| VersionId | Uuid | true |
| ScopeRevision | long | true |
| AssignmentId | Uuid | true |
| Reason | string | true |
