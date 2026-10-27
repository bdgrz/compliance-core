using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.provider.revision.get", 1)]
public sealed record GetProviderRevision(Uuid TenantId, Uuid ProviderId, long Revision)
    : IRequest<ProviderView>, IProviderRequest, ICallable;
