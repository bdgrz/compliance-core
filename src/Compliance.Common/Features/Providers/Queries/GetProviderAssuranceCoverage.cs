using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Queries;

/// <summary>Evaluates recorded due diligence and assurance at <c>AsOf</c> (default: today, UTC).</summary>
[Discriminator("bdgrz.provider.assurance_coverage.get", 1)]
public sealed record GetProviderAssuranceCoverage(Uuid TenantId, Uuid ProviderId, DateOnly? AsOf = null)
    : IRequest<ProviderAssuranceCoverageView>, IProviderRequest, ICallable;
