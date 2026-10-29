using System.Text.Json.Serialization;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AccessGrantPrincipalKind>))]
public enum AccessGrantPrincipalKind
{
    [JsonStringEnumMemberName("member")]
    Member,
    [JsonStringEnumMemberName("team")]
    Team,
}
