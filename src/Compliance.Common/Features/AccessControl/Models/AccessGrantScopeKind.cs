using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AccessGrantScopeKind>))]
public enum AccessGrantScopeKind
{
    [JsonStringEnumMemberName("organization")]
    Organization,
    [JsonStringEnumMemberName("program")]
    Program,
    [JsonStringEnumMemberName("engagement")]
    Engagement,
    [JsonStringEnumMemberName("shared_resource")]
    SharedResource,
    [JsonStringEnumMemberName("application")]
    Application,
    [JsonStringEnumMemberName("system_instance")]
    SystemInstance,
}
