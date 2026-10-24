using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>The record families whose active names are unique within a tenant and claimed by a name-claim owner.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NamedRecordKind>))]
public enum NamedRecordKind
{
    /// <summary>A provider; its governed name stays claimed until the provider is renamed.</summary>
    [JsonStringEnumMemberName("provider")]
    Provider,

    /// <summary>A location; active names are unique within the location kind.</summary>
    [JsonStringEnumMemberName("location")]
    Location,

    /// <summary>An operational process; active names are unique within the tenant.</summary>
    [JsonStringEnumMemberName("operational_process")]
    OperationalProcess,
}
