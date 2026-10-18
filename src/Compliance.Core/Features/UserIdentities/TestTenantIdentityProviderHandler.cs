using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Persists a revision-fenced request for the provider-test reactor.</summary>
public sealed class TestTenantIdentityProviderHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<TestTenantIdentityProvider>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<TestTenantIdentityProvider> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation,
                request.TenantId, out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Testing a tenant identity provider requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new TenantIdentityProvider(request.TenantId,
                request.ProviderConnectionId), provider =>
            CommandFailureRequestAdapter.ToOutcome(provider.RequestTest(request.ExpectedRevision,
                request.TestKey, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
