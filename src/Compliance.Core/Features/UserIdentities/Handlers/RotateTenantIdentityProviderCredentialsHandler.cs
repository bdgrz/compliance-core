using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

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
