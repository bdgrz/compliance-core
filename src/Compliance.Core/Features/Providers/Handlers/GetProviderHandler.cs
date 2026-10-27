using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

public sealed class GetProviderHandler(ProviderReadConsistency consistency) : IRequestHandler<GetProvider, ProviderView>
{
    public ValueTask<Result<ProviderView>> HandleAsync(IRequestContext<GetProvider> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProviderId, context.Request.MinimumRevision, ct);
}
