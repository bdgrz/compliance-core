using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The kind of engagement independence operation a frozen intent admits.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EngagementIndependenceOperationKind>))]
public enum EngagementIndependenceOperationKind
{
    /// <summary>The acceptance of a draft engagement with its proposed professional team.</summary>
    [JsonStringEnumMemberName("acceptance")]
    Acceptance,

    /// <summary>One positive actual staff assignment to an accepted engagement.</summary>
    [JsonStringEnumMemberName("staff_assignment")]
    StaffAssignment,
}
