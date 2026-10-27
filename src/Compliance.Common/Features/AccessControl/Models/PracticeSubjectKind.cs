using System.Text.Json.Serialization;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>The kind of subject a permanent practice commitment binds.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PracticeSubjectKind>))]
public enum PracticeSubjectKind
{
    /// <summary>A firm staff member.</summary>
    [JsonStringEnumMemberName("staff")]
    Staff,

    /// <summary>A canonical platform user.</summary>
    [JsonStringEnumMemberName("canonical_user")]
    CanonicalUser,
}
