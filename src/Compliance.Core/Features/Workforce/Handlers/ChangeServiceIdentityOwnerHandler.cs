using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ChangeServiceIdentityOwnerHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<ChangeServiceIdentityOwner>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeServiceIdentityOwner> context, CancellationToken ct)
    {
        var request = context.Request;
        var invalid = await ServiceIdentityOwners.ValidateAsync(reader, request.TenantId,
            request.OwnerKind, request.OwnerId, ct).ConfigureAwait(false);
        if (invalid is not null)
            return Result.Failure(invalid);
        return await executor.ExecuteAsync(new ServiceIdentity(request.TenantId, request.ServiceIdentityId),
            identity => CommandFailureRequestAdapter.ToOutcome(identity.ChangeOwner(request.ExpectedRevision,
                request.OwnerKind, request.OwnerId, WorkforceActor.From(context), clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
