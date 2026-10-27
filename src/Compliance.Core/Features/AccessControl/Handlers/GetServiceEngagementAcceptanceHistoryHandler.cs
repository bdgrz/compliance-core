using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

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
