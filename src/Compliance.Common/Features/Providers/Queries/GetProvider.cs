using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.provider.get", 1)]
public sealed record GetProvider(Uuid TenantId, Uuid ProviderId, long? MinimumRevision = null)
    : IRequest<ProviderView>, IProviderRequest, ICallable;
