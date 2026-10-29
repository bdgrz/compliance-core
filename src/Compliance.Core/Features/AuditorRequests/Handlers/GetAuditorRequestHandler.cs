using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

public sealed class GetAuditorRequestHandler(AuditorRequestReader requests) : IRequestHandler<GetAuditorRequest, AuditorRequestView>
{
    public ValueTask<Result<AuditorRequestView>> HandleAsync(IRequestContext<GetAuditorRequest> context, CancellationToken ct) =>
        AuditorRequestRead.ClassifyAsync<AuditorRequestView>(async () =>
        {
            var request = context.Request;
            if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.AuditorRequestId == Uuid.Empty)
                return Result<AuditorRequestView>.Failure(AuditorRequestRead.InvalidIdentity());
            var snapshot = await requests.ReadAsync(request.TenantId, request.ProgramId, request.AuditorRequestId, ct).ConfigureAwait(false);
            if (snapshot.View is { } found && (found.TenantId != request.TenantId || found.ProgramId != request.ProgramId ||
                found.AuditorRequestId != request.AuditorRequestId))
                return Result<AuditorRequestView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The auditor-request source has another exact owner."));
            return snapshot.View is { } view
                ? Result<AuditorRequestView>.Success(view)
                : Result<AuditorRequestView>.Failure(AuditorRequestRead.Missing());
        });
}
