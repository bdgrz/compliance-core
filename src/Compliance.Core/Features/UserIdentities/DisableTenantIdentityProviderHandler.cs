using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Disables one tenant identity-provider connection with personal administrator attribution.</summary>
public sealed class DisableTenantIdentityProviderHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<DisableTenantIdentityProvider>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DisableTenantIdentityProvider> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Disabling a tenant identity provider requires a personal administrator actor.")));

        var actorMemberId = RbacIds.Member(request.TenantId, userId);
        var actorDisplay = UserIdentityClaims.BdgrzDisplay(context.Actor, userId);
        return executor.ExecuteAsync(new TenantIdentityProvider(request.TenantId,
                request.ProviderConnectionId), provider =>
            CommandFailureRequestAdapter.ToOutcome(provider.Disable(request.ExpectedRevision,
                request.Rationale, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
