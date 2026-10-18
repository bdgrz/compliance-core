using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records a new encrypted credential reference and returns the connection to pending test.</summary>
public sealed class RotateTenantIdentityProviderCredentialsHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RotateTenantIdentityProviderCredentials>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<RotateTenantIdentityProviderCredentials> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation,
                request.TenantId, out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Tenant identity-provider credential rotation requires a personal HTTP actor.")));

        return executor.ExecuteAsync(new TenantIdentityProvider(request.TenantId,
                request.ProviderConnectionId), provider =>
            CommandFailureRequestAdapter.ToOutcome(provider.RotateCredentials(provider.Revision,
                request.EncryptedCredentialReference, request.RotationReference, actorMemberId,
                actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
