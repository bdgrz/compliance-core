using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class DisableServiceIdentityHandler(IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<DisableServiceIdentity>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DisableServiceIdentity> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WorkforceActor.From(context);
        var now = clock.GetUtcNow();
        return executor.ExecuteAsync(new ServiceIdentity(request.TenantId, request.ServiceIdentityId),
            identity => CommandFailureRequestAdapter.ToOutcome(identity.Disable(request.ExpectedRevision, request.Reason, actor, now)), context, ct);
    }
}
