using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

public sealed class GetDataFlowHandler(IAggregateReader aggregates,
    TechnologyInventoryReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<GetDataFlow, DataFlowView>
{
    public async ValueTask<Result<DataFlowView>> HandleAsync(
        IRequestContext<GetDataFlow> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum data flow revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await aggregates.HydrateAsync(new DataFlow(request.TenantId,
            request.DataFlowId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadFlowAsync(request.TenantId, userId,
                source.Id, source.Content?.Classification, ct).ConfigureAwait(false))
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        var flow = await consistency.GetFlowAsync(request.TenantId, request.DataFlowId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (flow.IsSuccess && !await visibility.CanReadFlowAsync(request.TenantId, userId, flow.Value, ct)
                .ConfigureAwait(false))
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        var current = await aggregates.HydrateAsync(new DataFlow(request.TenantId,
            request.DataFlowId), ct).ConfigureAwait(false);
        if (!current.IsCreated || !await visibility.CanReadFlowAsync(request.TenantId, userId,
                current.Id, current.Content?.Classification, ct).ConfigureAwait(false))
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        if (!flow.IsSuccess)
            return flow;
        if (current.Revision != flow.Value.Revision)
            return Result<DataFlowView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The data flow changed while it was being read. Retry the query.",
                isTransient: true));
        return flow;
    }
}
