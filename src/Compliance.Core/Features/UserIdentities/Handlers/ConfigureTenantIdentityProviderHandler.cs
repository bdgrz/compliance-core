using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
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
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

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
