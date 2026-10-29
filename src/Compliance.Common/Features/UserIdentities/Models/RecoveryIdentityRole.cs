using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

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
