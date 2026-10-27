using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Disables one tenant identity-provider connection with personal administrator attribution.</summary>
public sealed class DisableTenantIdentityProviderHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<DisableTenantIdentityProvider>
{
    const string CommandKind = nameof(DisableTenantIdentityProvider);

    public async ValueTask<Result> HandleAsync(IRequestContext<DisableTenantIdentityProvider> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Disabling a tenant identity provider requires a personal administrator actor."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderConnectionId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant and provider connection are required."));

        var now = clock.GetUtcNow();
        var rationale = request.Rationale?.Trim() ?? string.Empty;
        var provenance = TenantIdentityProviderOperations.Provenance(request.TenantId, request.ProviderConnectionId,
            request.CommandId, CommandKind, ActorReference.ForMember(actorMemberId, actorDisplay),
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request with { Rationale = rationale }), now);
        return await TenantIdentityProviderOperations.AdmitAsync(operations, content, context, provenance,
            request.TenantId, request.ProviderConnectionId,
            (TenantIdentityProvider provider) => provider.PrepareDisable(request.ExpectedRevision, rationale, actorMemberId, actorDisplay, now),
            (TenantIdentityProvider provider, ImmutableContentRef frozen) => provider.Disable(provenance, frozen, request.ExpectedRevision, rationale,
                actorMemberId, actorDisplay, now), ct).ConfigureAwait(false);
    }
}
