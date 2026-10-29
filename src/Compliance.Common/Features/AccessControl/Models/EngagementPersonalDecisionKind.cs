using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

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
