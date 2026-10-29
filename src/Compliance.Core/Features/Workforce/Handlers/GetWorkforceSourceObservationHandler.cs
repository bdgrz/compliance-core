using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
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
