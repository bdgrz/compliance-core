using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>The kind of parent admission a <see cref="ClosureIntent" /> closes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClosureParentKind>))]
public enum ClosureParentKind
{
    /// <summary>An access review campaign completion.</summary>
    [JsonStringEnumMemberName("access_review_campaign")]
    AccessReviewCampaign,
}
