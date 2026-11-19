using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>An exact committed event and its resource and common-area positions.</summary>
/// <param name="Stream">The concrete event source.</param>
/// <param name="ResourceOffset">The event's physical position on that source.</param>
/// <param name="AreaOffset">The broker-reported position in the source area.</param>
/// <param name="Event">The verified native event.</param>
public sealed record CriteriaOrderedSourceEvent(EventStreamAddress Stream, ulong ResourceOffset,
    ulong AreaOffset, DomainEvent Event)
{
    /// <summary>
    /// Optional broker-reported position in the realm. This observation does not by itself
    /// qualify source freshness, completeness, common lineage, or cross-resource causality.
    /// </summary>
    public ulong? RealmOffset { get; init; }
}
