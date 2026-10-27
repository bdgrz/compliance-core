using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
using Bdgrz.Compliance.Features.Providers.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Evaluates the complete report and review history of the provider from the assurance directory. The read
///     fails closed and transient while the directory is behind its source, so it never evaluates a partial history.
/// </summary>
public sealed class GetProviderAssuranceCoverageHandler(AssuranceHistoryReader history, AssuranceReferences references,
    TimeProvider clock) : IRequestHandler<GetProviderAssuranceCoverage, ProviderAssuranceCoverageView>
{
    public async ValueTask<Result<ProviderAssuranceCoverageView>> HandleAsync(
        IRequestContext<GetProviderAssuranceCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderAssuranceCoverageView>.Failure(provider.Error);
        var source = await history.ReadAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ProviderAssuranceCoverageView>.Failure(source.Error);
        var asOf = request.AsOf ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return Result<ProviderAssuranceCoverageView>.Success(ProviderAssuranceCoverage.Evaluate(provider.Value,
            source.Value.Reports, source.Value.Reviews, asOf));
    }
}
