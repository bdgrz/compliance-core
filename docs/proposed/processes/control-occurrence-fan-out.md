# ControlOccurrenceFanOut

- Root source events: DueControlOccurrencesGenerated
- Ordered stages: false

| Stage | Receivers | Rule |
| --- | --- | --- |
| materialize | MaterializeControlOccurrence | One receiver per selected window; canonical OccurrenceId dedupes generators. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/control-occurrence-fan-out.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
