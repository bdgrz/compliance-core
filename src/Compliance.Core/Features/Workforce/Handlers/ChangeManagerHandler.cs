using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ChangeManagerHandler(IAggregateExecutor executor, IAggregateReader reader,
    IPermissionAuthorizer permissions, TimeProvider clock)
    : IRequestHandler<ChangeManager>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeManager> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WorkforceActor.From(context);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        if (!managerChain.CanRead)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Changing a manager requires the restricted manager-chain field grant."));
        var current = await reader.HydrateAsync(new WorkRelationship(request.TenantId, request.RelationshipId), ct)
            .ConfigureAwait(false);
        if (current.IsCreated && request.ManagerPersonId is { } managerId && managerId != Uuid.Empty)
        {
            var manager = await reader.HydrateAsync(new Person(request.TenantId, managerId), ct).ConfigureAwait(false);
            if (!manager.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The manager must be a recorded person of this organization."));
        }
        return await executor.ExecuteAsync(new WorkRelationship(request.TenantId, request.RelationshipId),
            relationship => CommandFailureRequestAdapter.ToOutcome(relationship.ChangeManager(
                request.ExpectedRevision, request.ManagerPersonId, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
