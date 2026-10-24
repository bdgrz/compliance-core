using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>What the native admissions of the tenant identity-provider commands share.</summary>
static class TenantIdentityProviderOperations
{
    /// <summary>
    ///     Freezes the secret-store pointer of a credential epoch as protected tenant content. The content id derives
    ///     from the command and the pointer's digest, so an exact retry names the same content and a changed pointer
    ///     names different content instead of colliding with the first.
    /// </summary>
    public static Task<ImmutableContentRef> FreezeCredentialAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid commandId, string credentialReference, CancellationToken ct) =>
        content.WriteJsonAsync(tenantId.ToString(),
            Uuid.CreateVersion5(commandId, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credentialReference)))),
            TenantIdentityProviderCredentialContent.Schema,
            new TenantIdentityProviderCredentialContent(credentialReference), ct);

    /// <summary>The provenance of a root admission on the connection; a retry's binding is derived from it.</summary>
    public static OperationProvenance Provenance(Uuid tenantId, Uuid providerConnectionId, Uuid logicalId,
        string commandKind, ActorReference actor, long expectedRevision, string authoredIntentSha256,
        DateTimeOffset acceptedAt)
    {
        var owner = new TenantIdentityProvider(tenantId, providerConnectionId).Stream;
        return new OperationProvenance(logicalId, logicalId, null, commandKind, actor, owner, logicalId.ToString(),
            expectedRevision >= 0 ? (ulong)expectedRevision : null, authoredIntentSha256, acceptedAt, owner, owner);
    }

    /// <summary>Runs one root admission on the connection and returns its closed outcome as a handler result.</summary>
    public static async ValueTask<Result> AdmitAsync(NativeOperationExecutor operations,
        IImmutableContentStore content, IExecutionContext context, OperationProvenance provenance, Uuid tenantId,
        Uuid providerConnectionId, Func<TenantIdentityProvider, CommandFailure?> prepare,
        Func<TenantIdentityProvider, ImmutableContentRef, CommandFailure?> apply, CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(), UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
            UnitResult.Schema, new StandardResponseBody(unit));
        var accepted = await operations.ExecuteAsync(new NativeOperation<TenantIdentityProvider>(
            AcceptedOperationBinding.Of(provenance),
            () => new TenantIdentityProvider(tenantId, providerConnectionId, provenance.LogicalId,
                provenance.CommandKind),
            provider => provider.Acceptance,
            provider => prepare(provider) is { } failure
                ? Result<FirstResponseDescriptor>.Failure(failure.ToRequestError())
                : Result<FirstResponseDescriptor>.Success(response),
            (TenantIdentityProvider provider, ImmutableContentRef frozen) => apply(provider, frozen) is { } failure
                ? Result.Failure(failure.ToRequestError())
                : Result.Success), context, ct).ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return Result.Failure(accepted.Error!);
        return accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
    }
}
