using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.provider.coverage_gap.get", 1)]
public sealed record GetProviderCoverageGap(Uuid TenantId, Uuid ProviderId, Uuid GapId)
    : IRequest<ProviderCoverageGapView>, IProviderRequest, ICallable;
