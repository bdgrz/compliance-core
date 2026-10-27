using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
using Bdgrz.Compliance.Features.Providers.Readers;
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
