using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class GetWorkforceSourceObservationHandler(WorkforceSourceReadConsistency consistency,
    WorkforceSourceTargets targets, IPermissionAuthorizer permissions)
    : IRequestHandler<GetWorkforceSourceObservation, WorkforceSourceView>
{
    public async ValueTask<Result<WorkforceSourceView>> HandleAsync(
        IRequestContext<GetWorkforceSourceObservation> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await consistency.GetAsync(request.TenantId, request.ObservationId, request.MinimumRevision, ct)
            .ConfigureAwait(false);
        if (!source.IsSuccess)
            return source;
        var view = source.Value;
        var target = await targets.GetAsync(request.TenantId, view.TargetKind, view.TargetId, ct).ConfigureAwait(false);
        if (!target.IsSuccess)
            return Result<WorkforceSourceView>.Failure(target.Error);
        view = view with
        {
            CurrentTargetRevision = target.Value.Revision,
            AcceptedForCurrentRevision = view.Decision?.Outcome == "accepted" &&
                                         view.Decision.TargetRevision == target.Value.Revision,
        };
        if (view.Facts.WorkRelationship is not null)
        {
            var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
                ? subject : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
            var manager = await FieldRestrictions.ForActorAsync(permissions, request.TenantId, userId,
                FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
            var reason = await FieldRestrictions.ForActorAsync(permissions, request.TenantId, userId,
                FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
            view = WorkforceSourceRedaction.Apply(view, manager.CanRead, reason.CanRead);
        }
        return Result<WorkforceSourceView>.Success(view);
    }
}
