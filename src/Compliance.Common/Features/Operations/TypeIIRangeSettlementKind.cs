using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>What one calendar settlement did to the exact reservation it names.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TypeIIRangeSettlementKind>))]
public enum TypeIIRangeSettlementKind
{
    /// <summary>The pending reservation became the live range of its period.</summary>
    [JsonStringEnumMemberName("activated")]
    Activated,

    /// <summary>The live or pending reservation was released.</summary>
    [JsonStringEnumMemberName("released")]
    Released,

    /// <summary>The reservation was settled before it was ever granted; a late acquisition is refused.</summary>
    [JsonStringEnumMemberName("fenced")]
    Fenced,
}
