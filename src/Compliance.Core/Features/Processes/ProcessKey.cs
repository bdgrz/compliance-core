using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Identity of one durable process record, bound to its concrete committed root fact.</summary>
public sealed record ProcessKey(string ProcessKind, string Realm, Uuid RootOperationId, Uuid SourceId)
{
    /// <summary>Derives a stable source identity without assuming logical operation ids are unique across owners.</summary>
    public static ProcessKey ForRoot(string processKind, Uuid rootOperationId, NativeFactReference source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processKind);
        ArgumentNullException.ThrowIfNull(source);
        return new ProcessKey(processKind, source.Stream.Realm, rootOperationId,
            AggregateIdentity.Create("ProcessRecord", source.Stream.ToString(), source.EventId));
    }
}
