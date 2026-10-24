using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>The kind of parent a closure intent seals.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClosureParentKind>))]
public enum ClosureParentKind
{
    /// <summary>A finding.</summary>
    [JsonStringEnumMemberName("finding")]
    Finding,

    /// <summary>An auditor request.</summary>
    [JsonStringEnumMemberName("auditor_request")]
    AuditorRequest,

    /// <summary>An access review campaign.</summary>
    [JsonStringEnumMemberName("access_review_campaign")]
    AccessReviewCampaign,

    /// <summary>A policy campaign.</summary>
    [JsonStringEnumMemberName("policy_campaign")]
    PolicyCampaign,
}
