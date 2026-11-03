# IdentityRecoveryNoticeProcess commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/identity-recovery-notice-process.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### SendIdentityRecoveryNotice

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/UserIdentities/Commands/SendIdentityRecoveryNotice.cs](../../../src/Compliance.Common/Features/UserIdentities/Commands/SendIdentityRecoveryNotice.cs)
- Handler: [src/Compliance.Core/Features/UserIdentities/Handlers/SendIdentityRecoveryNoticeHandler.cs](../../../src/Compliance.Core/Features/UserIdentities/Handlers/SendIdentityRecoveryNoticeHandler.cs)
- Discriminator: `bdgrz.identity-recovery.send-notice@1`
- Request interfaces: IIdentityRecoveryStepReceiver
- Response: `ReceiverOutcome`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<IdentityRecoveryCompleted> | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ChallengeId | Uuid | true |
| Recipient | string | true |
