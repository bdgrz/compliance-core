using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>The closed set of parent owners whose closure drains admitted children and holds their source uses.</summary>
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

    /// <summary>A policy or training campaign.</summary>
    [JsonStringEnumMemberName("policy_campaign")]
    PolicyCampaign,
}
