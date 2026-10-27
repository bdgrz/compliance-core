using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Records a new credential epoch and returns the connection to pending test. The command identity is the caller's:
///     an exact retry replays the original acceptance even after later revisions, a changed pointer or rotation
///     reference on the same identity conflicts, and the pointer never enters the fact or the digest.
/// </summary>
public sealed class RotateTenantIdentityProviderCredentialsHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RotateTenantIdentityProviderCredentials>
{
    const string CommandKind = nameof(RotateTenantIdentityProviderCredentials);

    public async ValueTask<Result> HandleAsync(IRequestContext<RotateTenantIdentityProviderCredentials> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Tenant identity-provider credential rotation requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderConnectionId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant and provider connection are required."));
        if (!TenantIdentityProvider.ValidCredentialReference(request.EncryptedCredentialReference))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Credential rotation requires a valid secret-store reference."));

        var credential = await TenantIdentityProviderOperations.FreezeCredentialAsync(content, request.TenantId,
            request.CommandId, request.EncryptedCredentialReference.Trim(), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var rotationReference = request.RotationReference?.Trim() ?? string.Empty;
        var provenance = TenantIdentityProviderOperations.Provenance(request.TenantId, request.ProviderConnectionId,
            request.CommandId, CommandKind, ActorReference.ForMember(actorMemberId, actorDisplay),
            request.ExpectedRevision, AuthoredIntentDigest.Compute(
                new RotateTenantIdentityProviderCredentialsAuthoredIntent(request.TenantId,
                    request.ProviderConnectionId, credential.Sha256, rotationReference, request.ExpectedRevision)), now);
        return await TenantIdentityProviderOperations.AdmitAsync(operations, content, context, provenance,
            request.TenantId, request.ProviderConnectionId,
            (TenantIdentityProvider provider) => provider.PrepareRotate(request.ExpectedRevision, credential, rotationReference, actorMemberId,
                actorDisplay, now),
            (TenantIdentityProvider provider, ImmutableContentRef frozen) => provider.RotateCredentials(provenance, frozen, request.ExpectedRevision, credential,
                rotationReference, actorMemberId, actorDisplay, now), ct).ConfigureAwait(false);
    }
}
