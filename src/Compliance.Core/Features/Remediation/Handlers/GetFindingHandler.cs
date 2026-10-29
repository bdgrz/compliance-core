using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Remediation.Readers;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Reads one finding composed from its own owner streams.</summary>
public sealed class GetFindingHandler(FindingReader findings, TimeProvider clock)
    : IRequestHandler<GetFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<GetFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        return await findings.ReadOneAsync(request.TenantId, request.ProgramId, request.FindingId, clock.GetUtcNow(), ct)
            .ConfigureAwait(false);
    }
}
