using System.Text.Json.Serialization;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Whether a professional-duty change records or revokes a designation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProfessionalDutyChangeKind>))]
public enum ProfessionalDutyChangeKind
{
    /// <summary>A signed active assertion records or refreshes the designation.</summary>
    [JsonStringEnumMemberName("record")]
    Record,

    /// <summary>A signed revocation assertion ends the designation.</summary>
    [JsonStringEnumMemberName("revoke")]
    Revoke,
}
