using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>A single immutable-incarnation, unfiltered, exact-resource read session.</summary>
/// <remarks>
/// Records cover all offsets from zero to ExclusiveHead in this exact resource, without gaps,
/// truncation, filtering, resets or cross-incarnation stitching. IsCurrentAsync returns true only
/// when the bound session still proves that exact observed head is the current committed head,
/// including after ambiguous commits and failover. A quiet page does not establish this property.
/// This proof is point-in-time and does not authorize a later cross-owner conditional append.
/// </remarks>
public interface IPlatformOperatorQualifiedSourceSession : IAsyncDisposable
{
    /// <summary>The exact resource's exclusive committed head for this session.</summary>
    ulong ExclusiveHead { get; }

    /// <summary>Streams this session's unfiltered records from offset zero.</summary>
    IAsyncEnumerable<DomainEventRecord> ReadAsync(CancellationToken ct = default);

    /// <summary>Revalidates this very session and its head against the current source.</summary>
    ValueTask<bool> IsCurrentAsync(CancellationToken ct = default);
}
