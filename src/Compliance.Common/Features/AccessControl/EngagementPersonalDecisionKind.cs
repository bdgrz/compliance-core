using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The kind of personal engagement decision; it selects the required source uses and the child owner.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EngagementPersonalDecisionKind>))]
public enum EngagementPersonalDecisionKind
{
    /// <summary>The owning client's personal management acknowledgement.</summary>
    [JsonStringEnumMemberName("management_acknowledgement")]
    ManagementAcknowledgement,

    /// <summary>The designated partner's personal independence evaluation.</summary>
    [JsonStringEnumMemberName("partner_evaluation")]
    PartnerEvaluation,
}
