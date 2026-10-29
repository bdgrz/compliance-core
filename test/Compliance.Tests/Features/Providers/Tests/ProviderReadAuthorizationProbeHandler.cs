using Cntryl.Portia;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

sealed class ProviderReadAuthorizationProbeHandler : IRequestHandler<GetProvider, ProviderView>
{
    public ValueTask<Result<ProviderView>> HandleAsync(IRequestContext<GetProvider> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<ProviderView>.Success(new ProviderView(context.Request.TenantId,
            context.Request.ProviderId, 1, new ProviderContent("Provider", "Supplier"), "manual", "active", [],
            ActorReference.ForSystemProcess("probe", "Probe"), DateTimeOffset.UtcNow)));
}
