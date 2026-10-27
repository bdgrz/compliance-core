using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Accepts an invitation on the invitation owner the route's invitation id selects. An exact retry of an
///     accepted operation replays the original acceptance even after the offer's expiry; a changed actor or recipient
///     conflicts. The token is verified inside the owner and never enters the authored intent. After the acceptance
///     commits or replays, the mailbox claim of exactly this invitation and generation is released; a failed
///     release is returned so that the same retry completes it.
/// </summary>
public sealed class AcceptTenantInvitationHandler(NativeOperationExecutor operations, IAggregateReader reader,
    TenantInvitationMailboxes mailboxes, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AcceptTenantInvitation>
{
    /// <summary>Schema of the complete guest program scope an acceptance freezes.</summary>
    public const string GuestScopeSchema = "TenantInvitationAccepted.GuestProgramIdsManifest.v1";

    public async ValueTask<Result> HandleAsync(IRequestContext<AcceptTenantInvitation> context, CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("AcceptTenantInvitationAuthorizer must reject this actor.");
        if (request.InvitationId == Uuid.Empty || request.TenantId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "An invitation id and tenant are required.");
        if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
            return Failure(RequestErrorKind.Validation, "Enter a valid email address.");

        var logicalId = TenantInvitationIds.AcceptanceOf(request.InvitationId);
        var actor = RegisterMemberHandler.ActorOf(context.Actor)!;
        var owner = new TenantInvitation(request.TenantId, request.InvitationId).Stream;
        var now = clock.GetUtcNow();
        var provenance = new OperationProvenance(logicalId, logicalId, null, nameof(AcceptTenantInvitation), actor,
            owner, "initial", null, AuthoredIntentDigest.Compute(new TenantInvitationAcceptanceIntent(
                request.InvitationId, request.TenantId, normalized)), now, owner, owner);
        var binding = AcceptedOperationBinding.Of(provenance);
        var realm = request.TenantId.ToString();

        var current = await reader.HydrateAsync(Hydrate(request, logicalId), ct).ConfigureAwait(false);
        ImmutableContentRef? scope = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None &&
            current.GuestProgramIdsManifest is { } offered)
        {
            var programIds = await content.ReadJsonAsync<IReadOnlyList<Uuid>>(realm, offered, ct).ConfigureAwait(false);
            scope = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), GuestScopeSchema, programIds, ct)
                .ConfigureAwait(false);
        }

        var response = await TenantInvitationResponses.FreezeUnitAsync(content, realm, logicalId, ct)
            .ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TenantInvitation>(binding,
            () => Hydrate(request, logicalId), invitation => invitation.Acceptance,
            invitation =>
            {
                var prepared = invitation.PrepareAccept(userId, normalized, request.Token, now);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (TenantInvitation invitation, ImmutableContentRef frozen) => invitation.Accept(provenance, frozen, userId,
                normalized, request.Token, now, scope)), context, ct).ConfigureAwait(false);
        var result = TenantInvitationResponses.Of(accepted);
        if (!result.IsSuccess)
            return result;

        // The acceptance is durable; the claim of exactly this invitation and generation can now be released.
        // The invitation's own facts, not the request, name the mailbox and generation.
        return await mailboxes.ReleaseAsync(context, request.TenantId, current.EmailAddress!, request.InvitationId,
            current.MailboxGeneration, TenantInvitationMailboxClaim.AcceptedReason, ct).ConfigureAwait(false);
    }

    static TenantInvitation Hydrate(AcceptTenantInvitation request, Uuid logicalId) =>
        new(request.TenantId, request.InvitationId, logicalId);

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
