using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.provider.coverage_gap.get", 1)]
public sealed record GetProviderCoverageGap(Uuid TenantId, Uuid ProviderId, Uuid GapId)
    : IRequest<ProviderCoverageGapView>, IProviderRequest, ICallable;
