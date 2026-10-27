using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.providers.list", 1)]
public sealed record ListProviders(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProviderView>>, IProviderRequest, ICallable;
