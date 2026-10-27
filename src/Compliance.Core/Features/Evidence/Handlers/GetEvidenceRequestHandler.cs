using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Readers;
namespace Bdgrz.Compliance.Features.Evidence.Handlers;

/// <summary>Reads one evidence request from its own owner stream.</summary>
public sealed class GetEvidenceRequestHandler(EvidenceRequestReader requests)
    : IRequestHandler<GetEvidenceRequest, EvidenceRequestView>
{
    public async ValueTask<Result<EvidenceRequestView>> HandleAsync(IRequestContext<GetEvidenceRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return await requests.ReadViewAsync(request.TenantId, request.ProgramId, request.EvidenceRequestId, ct)
            .ConfigureAwait(false) is { } found
            ? Result<EvidenceRequestView>.Success(found)
            : Result<EvidenceRequestView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence request was not found."));
    }
}
