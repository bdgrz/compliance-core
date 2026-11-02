# ProviderNameChangeProcess

- Root source events: ProviderChangeAccepted
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| claim | ClaimProviderName | New then old key in canonical order; hold both through apply. |
| apply | ApplyProviderChange | [Placeholder: no stage-specific rule recorded] |
| release | ReleaseProviderName | Old matching generation only. |

Abort commands: CancelProviderChange

Complete selected process definition: [JSON](../data/processes/provider-name-change-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
