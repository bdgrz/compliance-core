# IdentityRecoveryProcess

- Root source events: IdentityRecoveryChallengeClaimed
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| prepare | MaterializeIdentityRecoveryPreparation | [Placeholder: no stage-specific rule recorded] |
| protect | ProtectRecoveryIdentity | Old and replacement slots in canonical key order. |
| accept | AcceptProtectedIdentityRecovery | Irreversible; no cancellation afterwards. |
| register | RegisterRecoveredIdentity | [Placeholder: no stage-specific rule recorded] |
| revoke | RevokeRecoveredOldIdentity | Only with authoritative replacement proof. |
| complete | CompleteAcceptedIdentityRecovery | [Placeholder: no stage-specific rule recorded] |
| settle | SettleRecoveryIdentityProtection, CompleteEmailRecoveryChallenge, SendIdentityRecoveryNotice | Independent; the notice cannot hold completion hostage. |

Abort commands: RejectIdentityRecoveryPreparation

Complete selected process definition: [JSON](../data/processes/identity-recovery-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
