using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>Why a source owner is held: the business decision that reads the source and needs it unchanged.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceUsePurpose>))]
public enum SourceUsePurpose
{
    /// <summary>A campaign completion reads the source.</summary>
    [JsonStringEnumMemberName("campaign_completion")]
    CampaignCompletion,

    /// <summary>A request resolution reads the source.</summary>
    [JsonStringEnumMemberName("request_resolution")]
    RequestResolution,

    /// <summary>A finding closure reads the source.</summary>
    [JsonStringEnumMemberName("finding_closure")]
    FindingClosure,

    /// <summary>A readiness decision reads the source.</summary>
    [JsonStringEnumMemberName("readiness_decision")]
    ReadinessDecision,

    /// <summary>A campaign closure reads the source.</summary>
    [JsonStringEnumMemberName("campaign_closure")]
    CampaignClosure,

    /// <summary>A provider review reads the source.</summary>
    [JsonStringEnumMemberName("provider_review")]
    ProviderReview,

    /// <summary>An action completion review reads the source.</summary>
    [JsonStringEnumMemberName("action_completion_review")]
    ActionCompletionReview,

    /// <summary>An engagement acceptance reads the source.</summary>
    [JsonStringEnumMemberName("engagement_acceptance")]
    EngagementAcceptance,

    /// <summary>An actual staff assignment reads the source.</summary>
    [JsonStringEnumMemberName("actual_staff_assignment")]
    ActualStaffAssignment,

    /// <summary>An engagement personal decision reads the source.</summary>
    [JsonStringEnumMemberName("engagement_personal_decision")]
    EngagementPersonalDecision,

    /// <summary>An artifact download reads the source.</summary>
    [JsonStringEnumMemberName("artifact_download")]
    ArtifactDownload,

    /// <summary>A professional duty designation is recorded against the source staff record.</summary>
    [JsonStringEnumMemberName("professional_duty_recording")]
    ProfessionalDutyRecording,

    /// <summary>A bulk decision batch holds the source and applies a provisional decision to it.</summary>
    [JsonStringEnumMemberName("bulk_decision")]
    BulkDecision,
}
