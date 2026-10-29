using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Processes.Processes;

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
