using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

/// <summary>Previews the records a provider renewal, material change, or termination may affect.</summary>
[Discriminator("bdgrz.provider.change.preview", 1)]
public sealed record PreviewProviderChange(Uuid TenantId, Uuid ProviderId, long ExpectedRevision,
    string ChangeKind, DateOnly EffectiveOn, string ChangeSummary)
    : IRequest<ProviderChangeImpactPreview>, IProviderManagementRequest, ICallable;
