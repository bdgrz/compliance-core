using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>The kind of independent child a parent admits and a receiver materializes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdmittedChildKind>))]
public enum AdmittedChildKind
{
    /// <summary>Advisor feedback on a handoff.</summary>
    [JsonStringEnumMemberName("advisor_feedback")]
    AdvisorFeedback,

    /// <summary>An auditor response.</summary>
    [JsonStringEnumMemberName("auditor_response")]
    AuditorResponse,

    /// <summary>An auditor comment.</summary>
    [JsonStringEnumMemberName("auditor_comment")]
    AuditorComment,

    /// <summary>A corrective action of a finding.</summary>
    [JsonStringEnumMemberName("corrective_action")]
    CorrectiveAction,

    /// <summary>A policy acknowledgement.</summary>
    [JsonStringEnumMemberName("policy_acknowledgement")]
    PolicyAcknowledgement,

    /// <summary>A training completion.</summary>
    [JsonStringEnumMemberName("training_completion")]
    TrainingCompletion,

    /// <summary>A campaign waiver.</summary>
    [JsonStringEnumMemberName("campaign_waiver")]
    CampaignWaiver,
}
