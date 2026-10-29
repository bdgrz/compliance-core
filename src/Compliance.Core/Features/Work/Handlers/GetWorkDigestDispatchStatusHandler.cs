using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Reads the durable digest dispatch ledger without exposing message or transport data.</summary>
public sealed class GetWorkDigestDispatchStatusHandler(WorkDigestHistoryReader histories)
    : IRequestHandler<GetWorkDigestDispatchStatus, WorkDigestDispatchStatusView>
{
    public ValueTask<Result<WorkDigestDispatchStatusView>> HandleAsync(
        IRequestContext<GetWorkDigestDispatchStatus> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<WorkDigestDispatchStatusView>> HandleCoreAsync(
        IRequestContext<GetWorkDigestDispatchStatus> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.MemberId == Uuid.Empty)
            return Result<WorkDigestDispatchStatusView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The work digest dispatch scope is incomplete."));

        var dispatch = await histories.ReadDispatchAsync(request.TenantId, request.MemberId, ct).ConfigureAwait(false);
        return Result<WorkDigestDispatchStatusView>.Success(dispatch.Read(request.WeekOf));
    }
}
