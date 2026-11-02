# AdministratorTransitionProcess

- Root source events: TenantManagerWithdrawalAccepted, TenantManagerRestorationAccepted, TenantManagerSourceMutationAdmitted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| apply | ApplyMemberWithdrawal, ApplyAdministratorAssignmentWithdrawal, ApplyMemberRestoration, ApplyAdministratorAssignmentRestoration, ApplyNeutralMemberSourceMutation, ApplyNeutralAdministratorAssignmentMutation | Exactly one receiver per guard fact, selected by TransitionKind; the guard fact is always first. |
| complete | CompleteAdministratorWithdrawal, CompleteAdministratorRestoration | Neutral mutations have no completion fact. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/administrator-transition-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
