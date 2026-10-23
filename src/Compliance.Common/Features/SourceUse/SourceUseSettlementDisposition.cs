using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>How a source use ends; it must match the verified terminal source event kind.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceUseSettlementDisposition>))]
public enum SourceUseSettlementDisposition
{
    /// <summary>The parent closed; its closure fence stays installed.</summary>
    [JsonStringEnumMemberName("parent_closed")]
    ParentClosed,

    /// <summary>The parent was canceled; settlement follows the parent cancellation fence.</summary>
    [JsonStringEnumMemberName("canceled")]
    Canceled,

    /// <summary>The irreversible decision happened; settlement releases after that historical decision.</summary>
    [JsonStringEnumMemberName("irreversible_decision")]
    IrreversibleDecision,
}
