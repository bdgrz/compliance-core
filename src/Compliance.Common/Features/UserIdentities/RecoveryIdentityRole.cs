using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Which side of an identity recovery a protected identity slot is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecoveryIdentityRole>))]
public enum RecoveryIdentityRole
{
    /// <summary>The identity that is being retired.</summary>
    [JsonStringEnumMemberName("old")]
    Old,

    /// <summary>The identity that takes over the account.</summary>
    [JsonStringEnumMemberName("replacement")]
    Replacement,
}
