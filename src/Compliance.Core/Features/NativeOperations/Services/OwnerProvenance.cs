using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Services;

/// <summary>Builds and checks the provenance of operations whose first, affected and revision owner is one stream.</summary>
public static class OwnerProvenance
{
    /// <summary>The provenance of a root operation on <paramref name="owner" />, which is its own first, affected and revision scope.</summary>
    public static OperationProvenance For(Aggregate owner, Uuid commandId, string commandKind, ActorReference actor,
        string episode, ulong? originalExpectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(actor);
        return new OperationProvenance(commandId, commandId, null, commandKind, actor, owner.Stream, episode,
            originalExpectedRevision, authoredIntentSha256, acceptedAt, owner.Stream, owner.Stream);
    }

    /// <summary>
    ///     Rejects a native fact that does not accept its own logical operation on <paramref name="owner" />; a
    ///     reducer calls this so a malformed stream fails closed instead of binding the wrong operation.
    /// </summary>
    public static void RequireOwn(Aggregate owner, Uuid commandId, OperationProvenance operation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(operation);
        if (commandId == Uuid.Empty || operation.LogicalId != commandId || operation.Owner != owner.Stream)
            throw new InvalidOperationException(
                $"A {owner.GetType().Name} fact must accept its own logical operation on this owner.");
    }
}
