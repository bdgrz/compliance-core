using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.provider.revision.get", 1)]
public sealed record GetProviderRevision(Uuid TenantId, Uuid ProviderId, long Revision)
    : IRequest<ProviderView>, IProviderRequest, ICallable;
