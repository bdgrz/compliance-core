using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records one tenant identity-provider configuration for later provider verification.</summary>
public sealed class ConfigureTenantIdentityProviderHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ConfigureTenantIdentityProvider>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<ConfigureTenantIdentityProvider> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation,
                request.TenantId, out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Tenant identity-provider configuration requires a personal HTTP actor.")));

        var configuration = new TenantIdentityProviderConfigurationContent(request.Issuer,
            request.ClientId, request.EncryptedCredentialReference, request.AllowedAudiences);
        return executor.ExecuteAsync(new TenantIdentityProvider(request.TenantId,
                request.ProviderConnectionId), provider =>
            CommandFailureRequestAdapter.ToOutcome(provider.Configure(configuration,
                actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
