# EmailChallengeDeliveryLedger commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/email-challenge-delivery-ledger.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### DeliverEmailChallenge

Delivers a verification challenge to its mailbox under the at-least-once challenge policy. The mailbox is read from the address the source fact belongs to, so neither the command nor the process record carries it.

- Source: [src/Compliance.Common/Features/UserIdentities/Commands/DeliverEmailChallenge.cs](../../../src/Compliance.Common/Features/UserIdentities/Commands/DeliverEmailChallenge.cs)
- Handler: [src/Compliance.Core/Features/UserIdentities/Handlers/DeliverEmailChallengeHandler.cs](../../../src/Compliance.Core/Features/UserIdentities/Handlers/DeliverEmailChallengeHandler.cs)
- Discriminator: `bdgrz.email-challenge-delivery.deliver-verification@1`
- Request interfaces: IEmailChallengeDeliveryReceiver
- Response: `ReceiverOutcome`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | NativeFactReference | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ChallengeId | Uuid | true |

### DeliverRecoveryChallenge

Delivers an identity recovery challenge to its verified mailbox under the at-least-once challenge policy. The mailbox is read from the address the source fact belongs to, so neither the command nor the process record carries it.

- Source: [src/Compliance.Common/Features/UserIdentities/Commands/DeliverRecoveryChallenge.cs](../../../src/Compliance.Common/Features/UserIdentities/Commands/DeliverRecoveryChallenge.cs)
- Handler: [src/Compliance.Core/Features/UserIdentities/Handlers/DeliverRecoveryChallengeHandler.cs](../../../src/Compliance.Core/Features/UserIdentities/Handlers/DeliverRecoveryChallengeHandler.cs)
- Discriminator: `bdgrz.email-challenge-delivery.deliver-recovery@1`
- Request interfaces: IEmailChallengeDeliveryReceiver
- Response: `ReceiverOutcome`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | NativeFactReference | true |
| Target | EventStreamAddress | true |
| OperationId | Uuid | true |
| Generation | long | true |
| ExpectedRevision | long? | true |
| Intent | ImmutableContentRef | true |
| ChallengeId | Uuid | true |

### RecordEmailChallengeDeliverySent

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/UserIdentities/Commands/RecordEmailChallengeDeliverySent.cs](../../../src/Compliance.Common/Features/UserIdentities/Commands/RecordEmailChallengeDeliverySent.cs)
- Handler: [src/Compliance.Core/Features/UserIdentities/Handlers/RecordEmailChallengeDeliverySentHandler.cs](../../../src/Compliance.Core/Features/UserIdentities/Handlers/RecordEmailChallengeDeliverySentHandler.cs)
- Discriminator: `bdgrz.email-challenge-delivery.record-sent@1`
- Request interfaces: IEmailChallengeDeliveryOutcome
- Response: `Unit`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| ChallengeId | Uuid | true |
| AttemptId | Uuid | true |
| SentAt | DateTimeOffset | true |

### RecordEmailChallengeDeliveryFailed

One delivery attempt of an email challenge did not end in an accepted message. result may still have reached the mailbox and is reconciled before another attempt.

- Source: [src/Compliance.Common/Features/UserIdentities/Commands/RecordEmailChallengeDeliveryFailed.cs](../../../src/Compliance.Common/Features/UserIdentities/Commands/RecordEmailChallengeDeliveryFailed.cs)
- Handler: [src/Compliance.Core/Features/UserIdentities/Handlers/RecordEmailChallengeDeliveryFailedHandler.cs](../../../src/Compliance.Core/Features/UserIdentities/Handlers/RecordEmailChallengeDeliveryFailedHandler.cs)
- Discriminator: `bdgrz.email-challenge-delivery.record-failed@1`
- Request interfaces: IEmailChallengeDeliveryOutcome
- Response: `Unit`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| ChallengeId | Uuid | true |
| AttemptId | Uuid | true |
| Certainty | string | true |
| FailureCode | string | true |
| FailedAt | DateTimeOffset | true |
