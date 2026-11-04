using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ReviewServiceIdentityHandler(IAggregateExecutor executor, IAggregateReader reader,
    IPermissionAuthorizer permissions, TimeProvider clock) : IRequestHandler<ReviewServiceIdentity>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewServiceIdentity> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A service identity review requires a personal HTTP invocation."));
        var request = context.Request;
        var current = await reader.HydrateAsync(new ServiceIdentity(request.TenantId, request.ServiceIdentityId), ct).ConfigureAwait(false);
        if (!current.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The service identity was not found."));
        // Bind this source authority check to the exact revision the executor will decide.
        if (current.Revision != request.ExpectedRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The service identity revision changed; review its current owner."));
        var reviewedAt = clock.GetUtcNow();
        if (!await ServiceIdentityReviewAccess.IsAllowedAsync(reader, permissions, request.TenantId, current, userId,
                DateOnly.FromDateTime(reviewedAt.UtcDateTime), ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the current owner or an authorized service identity reviewer may review this identity."));
        return await executor.ExecuteAsync(new ServiceIdentity(request.TenantId, request.ServiceIdentityId),
            identity => CommandFailureRequestAdapter.ToOutcome(identity.Review(request.ExpectedRevision,
                request.NextReviewDate, request.Notes, WorkforceActor.From(context), reviewedAt)), context, ct).ConfigureAwait(false);
    }
}
