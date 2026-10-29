using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ReconcileWorkforceSourceObservationHandler(IAggregateExecutor executor,
    IAggregateReader reader, WorkforceSourceTargets targets, TimeProvider clock, IPermissionAuthorizer permissions)
    : IRequestHandler<ReconcileWorkforceSourceObservation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReconcileWorkforceSourceObservation> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Workforce decision requires a personal HTTP invocation."));
        var request = context.Request;
        var source = await reader.HydrateAsync(new WorkforceSourceObservation(request.TenantId,
            request.ObservationId), ct).ConfigureAwait(false);
        if (source.Observation is not { } observed)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The source observation was not found."));
        var access = await WorkforceSourceComparisonAccess.CheckAsync(permissions, request.TenantId,
            context.Actor, observed.TargetKind == "work_relationship", ct).ConfigureAwait(false);
        if (!access.IsSuccess)
            return access;
        var target = source.Decision is not null
            ? Result<WorkforceSourceTarget>.Success(new WorkforceSourceTarget(source.Decision.TargetRevision, observed.Facts))
            : await targets.GetAsync(request.TenantId, observed.TargetKind, observed.TargetId, ct).ConfigureAwait(false);
        if (!target.IsSuccess)
            return Result.Failure(target.Error);
        return await executor.ExecuteAsync(new WorkforceSourceObservation(request.TenantId, request.ObservationId),
            observation => CommandFailureRequestAdapter.ToOutcome(observation.Reconcile(request.ExpectedRevision,
                request.ExpectedTargetRevision, target.Value.Revision, target.Value.Facts, request.Outcome,
                request.Note, WorkforceActor.From(context), clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
