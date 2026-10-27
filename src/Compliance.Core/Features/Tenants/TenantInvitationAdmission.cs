using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The shared first-owner admission of the three invitation commands. The first owner is the invitation owner
///     selected by the route's invitation id alone. A retry resolves against the invitation's own accepted facts
///     first and never touches the mailbox; a new invitation validates its terms, acquires the mailbox claim, freezes
///     the scope manifest and the first response, and only then publishes the invitation fact.
/// </summary>
public sealed class TenantInvitationAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    TenantInvitationMailboxes mailboxes, IImmutableContentStore content, EmailChallengeTokenKeys tokenKeys,
    TimeProvider clock)
{
    /// <summary>Schema of the complete guest program scope an invitation freezes.</summary>
    public const string GuestScopeSchema = "TenantMemberInvited.GuestProgramIdsManifest.v1";

    /// <summary>Admits one invitation creation.</summary>
    public async ValueTask<Result> AdmitAsync(IExecutionContext context, Uuid invitationId, Uuid tenantId,
        string commandKind, string authoredIntentSha256, TenantInvitationTerms terms, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terms);
        if (invitationId == Uuid.Empty || tenantId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "An invitation id and tenant are required.");
        if (!EmailAddresses.TryNormalize(terms.EmailAddress, out var normalized))
            return Failure(RequestErrorKind.Validation, "Enter a valid email address.");
        terms = terms with { EmailAddress = normalized };
        var actor = RegisterMemberHandler.ActorOf(context.Actor);
        if (actor is null)
            return Failure(RequestErrorKind.Forbidden, "The caller identity is not recognized.");

        var now = clock.GetUtcNow();
        var expiresAt = now + TenantInvitation.Lifetime;
        var keyId = tokenKeys.ActiveKeyId;
        var token = tokenKeys.DeriveInvitation(keyId, invitationId, tenantId, normalized, expiresAt);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var owner = new TenantInvitation(tenantId, invitationId).Stream;
        var provenance = new OperationProvenance(invitationId, invitationId, null, commandKind, actor, owner,
            "initial", null, authoredIntentSha256, now, owner, owner);
        var binding = AcceptedOperationBinding.Of(provenance);

        var generation = 0L;
        var current = await reader.HydrateAsync(new TenantInvitation(tenantId, invitationId, invitationId), ct)
            .ConfigureAwait(false);
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = current.PrepareInvite(terms, hash, expiresAt, now);
            if (!prepared.IsSuccess)
                return prepared;
            var claimed = await mailboxes.AcquireAsync(context, tenantId, normalized, invitationId, ct)
                .ConfigureAwait(false);
            if (!claimed.IsSuccess)
                return Result.Failure(claimed.Error!);
            generation = claimed.Value;
        }

        // Each candidate gets its own content id: a changed scope under the same invitation id must reach the
        // owner's conflict decision rather than collide with the first scope content.
        ImmutableContentRef? scope = null;
        if (terms.GuestProgramIds is { } programIds && generation > 0)
            scope = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(), GuestScopeSchema, programIds,
                ct).ConfigureAwait(false);
        var response = await TenantInvitationResponses.FreezeUnitAsync(content, tenantId.ToString(), invitationId, ct)
            .ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TenantInvitation>(binding,
            () => new TenantInvitation(tenantId, invitationId, invitationId), invitation => invitation.Acceptance,
            invitation =>
            {
                var checkedTerms = invitation.PrepareInvite(terms, hash, expiresAt, now);
                return checkedTerms.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(checkedTerms.Error!);
            },
            (TenantInvitation invitation, ImmutableContentRef frozen) => invitation.Invite(provenance, frozen, terms, hash,
                keyId, expiresAt, now, generation, scope)), context, ct).ConfigureAwait(false);
        return TenantInvitationResponses.Of(accepted);
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
