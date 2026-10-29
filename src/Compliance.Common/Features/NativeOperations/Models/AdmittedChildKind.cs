using System.Text.Json.Serialization;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>The closed set of child results a parent owner admits before an independent receiver applies them.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdmittedChildKind>))]
public enum AdmittedChildKind
{
    /// <summary>An advisor feedback entry.</summary>
    [JsonStringEnumMemberName("advisor_feedback")]
    AdvisorFeedback,

    /// <summary>An auditor response.</summary>
    [JsonStringEnumMemberName("auditor_response")]
    AuditorResponse,

    /// <summary>An auditor comment.</summary>
    [JsonStringEnumMemberName("auditor_comment")]
    AuditorComment,

    /// <summary>A corrective action.</summary>
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
