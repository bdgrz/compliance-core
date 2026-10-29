using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Reads every retained accepted snapshot of an engagement, oldest first, from its own owner.</summary>
public sealed class GetServiceEngagementAcceptanceHistoryHandler(IAggregateReader reader,
    IImmutableContentStore content, IDomainEventReader events)
    : IRequestHandler<GetServiceEngagementAcceptanceHistory, IReadOnlyList<ServiceEngagementAcceptanceView>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceEngagementAcceptanceView>>> HandleAsync(
        IRequestContext<GetServiceEngagementAcceptanceHistory> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty)
            return Result<IReadOnlyList<ServiceEngagementAcceptanceView>>.Success([]);
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(request.TenantId, request.EngagementId),
            ct).ConfigureAwait(false);
        var realm = IndependenceContent.Realm(request.TenantId);
        var snapshot = await ServiceEngagementHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var history = new List<ServiceEngagementAcceptanceView>(snapshot.AcceptanceHistory.Count);
        foreach (var result in snapshot.AcceptanceHistory)
            history.Add(await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm,
                result, ct).ConfigureAwait(false));
        return Result<IReadOnlyList<ServiceEngagementAcceptanceView>>.Success(Array.AsReadOnly(history.ToArray()));
    }
}
