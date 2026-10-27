using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Proofs.Models;

/// <summary>
/// Locates one concrete native event. The three positions are distinct broker offsets and AggregateVersion is the
/// metadata replay version, never the public logical revision.
/// </summary>
public sealed record SourceFactLocator(EventStreamAddress Source, Uuid EventId, ulong ResourcePosition,
    ulong RealmPosition, ulong GlobalPosition, ulong AggregateVersion, string PayloadSha256);
