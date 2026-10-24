using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Records one tenant identity-provider configuration for later provider verification. The command identity is the
///     caller's: an exact retry replays the original acceptance, a changed definition on the same identity conflicts,
///     and the secret-store pointer is frozen as protected content instead of entering the fact or the digest.
/// </summary>
public sealed class ConfigureTenantIdentityProviderHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ConfigureTenantIdentityProvider>
{
    const string CommandKind = nameof(ConfigureTenantIdentityProvider);

    public async ValueTask<Result> HandleAsync(IRequestContext<ConfigureTenantIdentityProvider> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Tenant identity-provider configuration requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderConnectionId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant and provider connection are required."));
        if (TenantIdentityProvider.NormalizeConfiguration(request.Issuer, request.ClientId,
                request.EncryptedCredentialReference, request.AllowedAudiences) is not { } configuration)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant identity provider requires a secure issuer, matching audience, client ID, and secret-store reference."));

        var credential = await TenantIdentityProviderOperations.FreezeCredentialAsync(content, request.TenantId,
            request.CommandId, configuration.CredentialReference, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var actor = ActorReference.ForMember(actorMemberId, actorDisplay);
        var provenance = TenantIdentityProviderOperations.Provenance(request.TenantId, request.ProviderConnectionId,
            request.CommandId, CommandKind, actor, request.ExpectedRevision,
            AuthoredIntentDigest.Compute(new ConfigureTenantIdentityProviderAuthoredIntent(request.TenantId,
                request.ProviderConnectionId, configuration.Issuer, configuration.ClientId,
                configuration.AllowedAudiences, credential.Sha256, request.ExpectedRevision)), now);
        return await TenantIdentityProviderOperations.AdmitAsync(operations, content, context, provenance,
            request.TenantId, request.ProviderConnectionId,
            (TenantIdentityProvider provider) => provider.PrepareConfigure(request.ExpectedRevision, actorMemberId, actorDisplay, now),
            (TenantIdentityProvider provider, ImmutableContentRef frozen) => provider.Configure(provenance, frozen, request.ExpectedRevision, configuration,
                credential, actorMemberId, actorDisplay, now), ct).ConfigureAwait(false);
    }
}
