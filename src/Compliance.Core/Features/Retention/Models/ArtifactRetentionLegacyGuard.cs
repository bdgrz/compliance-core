using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>Fails closed when a source still has combined-owner history for which no migration is selected.</summary>
static class ArtifactRetentionLegacyGuard
{
    public static async ValueTask<Result> RequireNoLegacyAsync(IDomainEventReader events, ArtifactRetentionSource source,
        CancellationToken ct)
    {
        var id = Uuid.CreateVersion5(source.TenantId, $"artifact_retention:{source.SourceKind}:{source.SourceId}");
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(source.TenantId.ToString(),
                           "artifact_retention"), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Stream.Resource == id.ToString() &&
                record.Event is ArtifactRetentionBound or ArtifactRetentionBasisRecorded or ArtifactLegalHoldPlaced or ArtifactLegalHoldReleased)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "This source has combined retention history; its verified owner migration is required before retention decisions or reads.", true));
        return Result.Success;
    }
}
