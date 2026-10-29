using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>
/// Locally known identity of a committed native fact: its owner stream, event id and metadata aggregate version.
/// Resource, realm and global broker positions are added by the source-fact resolver when a proof is built.
/// </summary>
public sealed record NativeFactReference(EventStreamAddress Stream, Uuid EventId, ulong AggregateVersion)
{
    /// <summary>References a committed fact by its attached metadata.</summary>
    public static NativeFactReference Of(EventStreamAddress stream, DomainEvent fact)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(fact);
        var metadata = fact.Metadata ?? throw new InvalidOperationException("The fact carries no metadata.");
        return new NativeFactReference(stream, metadata.EventId, metadata.AggregateVersion);
    }
}
