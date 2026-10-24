using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Whether a Type II range operation opens a period or amends the range of an open period.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TypeIIRangeOperationKind>))]
public enum TypeIIRangeOperationKind
{
    /// <summary>The first range of a new period.</summary>
    [JsonStringEnumMemberName("opening")]
    Opening,

    /// <summary>A replacement range of an open period; the old range stays held until it is applied.</summary>
    [JsonStringEnumMemberName("amendment")]
    Amendment,
}
