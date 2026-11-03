# WorkforceRosterSnapshotProcess commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/workforce-roster-snapshot-process.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### FreezeWorkforceRosterSnapshot

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Workforce/Commands/FreezeWorkforceRosterSnapshot.cs](../../../src/Compliance.Common/Features/Workforce/Commands/FreezeWorkforceRosterSnapshot.cs)
- Handler: [src/Compliance.Core/Features/Workforce/Handlers/FreezeWorkforceRosterSnapshotHandler.cs](../../../src/Compliance.Core/Features/Workforce/Handlers/FreezeWorkforceRosterSnapshotHandler.cs)
- Discriminator: `bdgrz.snapshot.workforce_roster.freeze@1`
- Request interfaces: IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
- Response: `SnapshotRegistration`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |

### AmendWorkforceRosterSnapshot

Freezes the current roster as an attributable amendment of an earlier roster snapshot. The earlier snapshot is never changed.

- Source: [src/Compliance.Common/Features/Workforce/Commands/AmendWorkforceRosterSnapshot.cs](../../../src/Compliance.Common/Features/Workforce/Commands/AmendWorkforceRosterSnapshot.cs)
- Handler: [src/Compliance.Core/Features/Workforce/Handlers/AmendWorkforceRosterSnapshotHandler.cs](../../../src/Compliance.Core/Features/Workforce/Handlers/AmendWorkforceRosterSnapshotHandler.cs)
- Discriminator: `bdgrz.snapshot.workforce_roster.amend@1`
- Request interfaces: IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
- Response: `SnapshotRegistration`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| SnapshotId | Uuid | true |
| Reason | string | true |
