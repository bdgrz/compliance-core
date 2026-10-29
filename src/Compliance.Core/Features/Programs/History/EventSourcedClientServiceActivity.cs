using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Aggregates;
namespace Bdgrz.Compliance.Features.Programs.History;

sealed class EventSourcedClientServiceActivity(IAggregateReader reader) : IClientServiceActivity
{
    public async ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid programId, Uuid serviceId,
        CancellationToken ct = default)
    {
        var service = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct)
            .ConfigureAwait(false);
        return service.IsActive && service.ProgramId == programId;
    }
}
