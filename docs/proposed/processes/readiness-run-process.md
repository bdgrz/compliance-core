# ReadinessRunProcess

- Root source events: ReadinessAssessmentRequested
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| compute | ComputeReadinessAssessment | Pure child computation from the frozen snapshot. |
| adopt | AdoptReadinessAssessment | Exact pending adoption generation. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/readiness-run-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
