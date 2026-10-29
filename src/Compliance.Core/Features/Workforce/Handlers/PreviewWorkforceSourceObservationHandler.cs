using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class PreviewWorkforceSourceObservationHandler(IAggregateReader reader,
    WorkforceSourceTargets targets, IPermissionAuthorizer permissions)
    : IRequestHandler<PreviewWorkforceSourceObservation, WorkforceSourcePreview>
{
    public async ValueTask<Result<WorkforceSourcePreview>> HandleAsync(
        IRequestContext<PreviewWorkforceSourceObservation> context, CancellationToken ct)
    {
        var request = context.Request;
        var observation = await reader.HydrateAsync(new WorkforceSourceObservation(request.TenantId,
            request.ObservationId), ct).ConfigureAwait(false);
        if (observation.Observation is not { } observed)
            return Result<WorkforceSourcePreview>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The source observation was not found."));
        var access = await WorkforceSourceComparisonAccess.CheckAsync(permissions, request.TenantId,
            context.Actor, observed.TargetKind == "work_relationship", ct).ConfigureAwait(false);
        if (!access.IsSuccess)
            return Result<WorkforceSourcePreview>.Failure(access.Error);
        var target = await targets.GetAsync(request.TenantId, observed.TargetKind, observed.TargetId, ct)
            .ConfigureAwait(false);
        return target.IsSuccess
            ? Result<WorkforceSourcePreview>.Success(WorkforceSourceReconciliation.Preview(observed,
                observation.Revision, target.Value.Revision, target.Value.Facts, observation.Decision))
            : Result<WorkforceSourcePreview>.Failure(target.Error);
    }
}
