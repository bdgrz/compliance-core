using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Persists a revision-fenced request for the provider-test worker. The logical identity is derived from the
///     connection and the exact test key, so an exact retry replays the original request even after the test finished.
/// </summary>
public sealed class TestTenantIdentityProviderHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<TestTenantIdentityProvider>
{
    const string CommandKind = nameof(TestTenantIdentityProvider);

    public async ValueTask<Result> HandleAsync(IRequestContext<TestTenantIdentityProvider> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!TenantIdentityProviderCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Testing a tenant identity provider requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProviderConnectionId == Uuid.Empty ||
            string.IsNullOrEmpty(request.TestKey))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, provider connection and test key are required."));

        var now = clock.GetUtcNow();
        var logicalId = TestTenantIdentityProvider.TestId(request.ProviderConnectionId, request.TestKey);
        var provenance = TenantIdentityProviderOperations.Provenance(request.TenantId, request.ProviderConnectionId,
            logicalId, CommandKind, ActorReference.ForMember(actorMemberId, actorDisplay), request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now);
        return await TenantIdentityProviderOperations.AdmitAsync(operations, content, context, provenance,
            request.TenantId, request.ProviderConnectionId,
            (TenantIdentityProvider provider) => provider.PrepareRequestTest(request.ExpectedRevision, request.TestKey, actorMemberId,
                actorDisplay, now),
            (TenantIdentityProvider provider, ImmutableContentRef frozen) => provider.RequestTest(provenance, frozen, request.ExpectedRevision, request.TestKey,
                actorMemberId, actorDisplay, now), ct).ConfigureAwait(false);
    }
}
