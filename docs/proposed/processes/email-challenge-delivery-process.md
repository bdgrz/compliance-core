# EmailChallengeDeliveryProcess

- Root source events: EmailChallengeIssued, IdentityRecoveryChallengeIssued
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| deliver | DeliverEmailChallenge, DeliverRecoveryChallenge | At-least-once; an unknown result is reconciled before the same message is resent. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/email-challenge-delivery-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
