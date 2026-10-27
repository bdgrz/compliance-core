using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

/// <summary>Evaluates recorded due diligence and assurance at <c>AsOf</c> (default: today, UTC).</summary>
[Discriminator("bdgrz.provider.assurance_coverage.get", 1)]
public sealed record GetProviderAssuranceCoverage(Uuid TenantId, Uuid ProviderId, DateOnly? AsOf = null)
    : IRequest<ProviderAssuranceCoverageView>, IProviderRequest, ICallable;
