using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetPrimaryOrgAdminsHandler(IAggregateReader reader) : IRequestHandler<GetPrimaryOrgAdmins, PrimaryOrgAdminsView>
{
    public async ValueTask<Result<PrimaryOrgAdminsView>> HandleAsync(IRequestContext<GetPrimaryOrgAdmins> context, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new PrimaryOrgAdmins(context.Request.TenantId), ct).ConfigureAwait(false);
        return Result<PrimaryOrgAdminsView>.Success(new(context.Request.TenantId, owner.Seats));
    }
}
