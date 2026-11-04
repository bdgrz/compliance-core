using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ChangeServiceIdentityPurposeHandler(IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<ChangeServiceIdentityPurpose>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ChangeServiceIdentityPurpose> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WorkforceActor.From(context);
        var now = clock.GetUtcNow();
        return executor.ExecuteAsync(new ServiceIdentity(request.TenantId, request.ServiceIdentityId),
            identity => CommandFailureRequestAdapter.ToOutcome(identity.ChangePurpose(request.ExpectedRevision, request.Purpose, actor, now)), context, ct);
    }
}
