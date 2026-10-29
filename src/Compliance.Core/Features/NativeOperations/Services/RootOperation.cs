using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Services;

/// <summary>Builds the provenance of a public command that is the root operation of its own first owner.</summary>
public static class RootOperation
{
    /// <summary>The episode of every first-owner operation of a public command.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>
    /// The provenance of a root operation whose first, affected and expected-revision owner is the same stream.
    /// <paramref name="expectedRevision"/> is null for a creation that has no caller revision.
    /// </summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        ActorReference actor, long? expectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(actor);
        if (expectedRevision is < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "An expected revision cannot be negative.");
        return new OperationProvenance(logicalId, logicalId, null, commandKind, actor, owner, InitialEpisode,
            expectedRevision is { } revision ? (ulong)revision : null, authoredIntentSha256, acceptedAt, owner, owner);
    }
}
