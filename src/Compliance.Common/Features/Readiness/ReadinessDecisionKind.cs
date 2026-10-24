using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The kind of management decision a readiness decision intent freezes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadinessDecisionKind>))]
public enum ReadinessDecisionKind
{
    /// <summary>The proceed or do_not_proceed readiness decision.</summary>
    [JsonStringEnumMemberName("readiness")]
    Readiness,

    /// <summary>The approve, approve_with_exceptions or defer Type I entry decision.</summary>
    [JsonStringEnumMemberName("type_i_entry")]
    TypeIEntry,
}
