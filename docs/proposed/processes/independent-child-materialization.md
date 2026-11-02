# IndependentChildMaterialization

- Root source events: AdvisorFeedbackAdmitted, AuditorResponseAdmitted, AuditorCommentAdmitted, CorrectiveActionAdmitted, AuditorSampleSelectionAdmitted, ProviderCoverageGapRiskAcceptanceAdmitted, PolicyParticipantResultAdmitted
- Ordered stages: false

| Stage | Receivers | Rule |
| --- | --- | --- |
| materialize | MaterializeAdvisorFeedback, MaterializeAuditorResponse, MaterializeAuditorComment, MaterializeCorrectiveAction, MaterializeAuditorSampleSelection, MaterializeProviderRiskAcceptanceLink, ApplyPolicyParticipantResult | Single receiver per admitted child; no cross-receiver ordering. Parent closure drains these before its own acquisition stage. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/independent-child-materialization.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
