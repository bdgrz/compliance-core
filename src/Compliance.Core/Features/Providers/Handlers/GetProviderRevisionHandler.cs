using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

public sealed class GetProviderRevisionHandler(ProviderReadConsistency consistency) : IRequestHandler<GetProviderRevision, ProviderView>
{
    public ValueTask<Result<ProviderView>> HandleAsync(IRequestContext<GetProviderRevision> context, CancellationToken ct) =>
        consistency.GetRevisionAsync(context.Request.TenantId, context.Request.ProviderId, context.Request.Revision, ct);
}
