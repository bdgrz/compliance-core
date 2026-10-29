using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Performs the external send of the delivery process for one frozen payload. The attempt is begun and stored
///     before the send with a stable identity, and the provider result is stored on that attempt afterwards. A crash
///     between the send and the stored result resumes with the same attempt, so the message keeps its identity and
///     the accepted at-least-once policy applies; it never starts a second attempt. A definite rejection is stored as
///     a failure and is not retried automatically.
/// </summary>
public sealed class SendTenantInvitationHandler(TenantInvitationDeliverySupport support, IImmutableContentStore content,
    IAggregateReader reader, ITenantInvitationDelivery delivery, EmailChallengeTokenKeys tokenKeys, TimeProvider clock)
    : IRequestHandler<SendTenantInvitation, ReceiverOutcome>
{
    /// <summary>Failure code stored when the protected key cannot reproduce the frozen token.</summary>
    public const string KeyUnavailable = "key_unavailable";

    /// <summary>Failure code stored when the provider rejected or could not take the message.</summary>
    public const string DeliveryFailed = "delivery_failed";

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<SendTenantInvitation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var invited = verified.Value!.Facts;
        var reference = TenantInvitationDeliverySupport.Reference(verified.Value);
        var payload = await ReadPayloadAsync(request, invited, ct).ConfigureAwait(false);
        if (payload is null)
            return TenantInvitationDeliverySupport.Invalid("The frozen payload does not match the invitation.");

        var attemptId = TenantInvitationDeliveryRecord.AttemptIdFor(request.InvitationId);
        var record = await support.UpdateAsync(request.TenantId, request.InvitationId, static current => current, ct)
            .ConfigureAwait(false);
        if (record.IsTerminal)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(reference));
        if (record.Attempt(attemptId) is { Result: not TenantInvitationDeliveryAttempt.InFlight })
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(reference));

        var moot = await MootReasonAsync(request, invited, ct).ConfigureAwait(false);
        if (moot is not null)
            return TenantInvitationDeliverySupport.Superseded(moot);

        var now = clock.GetUtcNow();
        await support.UpdateAsync(request.TenantId, request.InvitationId, current => current.Begin(attemptId, now), ct)
            .ConfigureAwait(false);
        var (result, code) = await SendAsync(attemptId, payload, ct).ConfigureAwait(false);
        await support.UpdateAsync(request.TenantId, request.InvitationId,
            current => current.Attempt(attemptId) is { Result: TenantInvitationDeliveryAttempt.InFlight }
                ? current.WithResult(attemptId, result, code, clock.GetUtcNow())
                : current, ct).ConfigureAwait(false);
        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(reference));
    }

    async Task<TenantInvitationDeliveryPayload?> ReadPayloadAsync(SendTenantInvitation request, TenantMemberInvited invited,
        CancellationToken ct)
    {
        try
        {
            var payload = await content.ReadJsonAsync<TenantInvitationDeliveryPayload>(request.TenantId.ToString(),
                request.Payload, ct).ConfigureAwait(false);
            return payload == TenantInvitationDeliveryPayload.Of(invited) ? payload : null;
        }
        catch (ImmutableContentIntegrityException)
        {
            return null;
        }
    }

    /// <summary>Why the send no longer applies: the offer expired, was accepted, or lost its mailbox to a newer invitation.</summary>
    async Task<string?> MootReasonAsync(SendTenantInvitation request, TenantMemberInvited invited, CancellationToken ct)
    {
        if (clock.GetUtcNow() >= invited.ExpiresAt)
            return "expired";
        var invitation = await reader.HydrateAsync(new TenantInvitation(request.TenantId, request.InvitationId), ct)
            .ConfigureAwait(false);
        if (invitation.IsAccepted)
            return "accepted";
        if (invitation.IsExpiredAt(clock.GetUtcNow()))
            return "expired";
        var claim = await reader.HydrateAsync(new TenantInvitationMailboxClaim(request.TenantId, invited.EmailAddress), ct)
            .ConfigureAwait(false);
        return claim.Holder != Uuid.Empty && claim.Holder != request.InvitationId ? "superseded" : null;
    }

    async Task<(string Result, string? FailureCode)> SendAsync(Uuid attemptId, TenantInvitationDeliveryPayload payload,
        CancellationToken ct)
    {
        // The payload carries only the token digest; the key must reproduce exactly the token that was frozen.
        if (payload.TokenKeyId is null ||
            !tokenKeys.TryDeriveInvitation(payload.TokenKeyId, payload.InvitationId, payload.TenantId,
                payload.EmailAddress, payload.ExpiresAt, out var token) ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(token!)),
                Convert.FromHexString(payload.TokenHash)))
            return (TenantInvitationDeliveryAttempt.Rejected, KeyUnavailable);

        try
        {
            await delivery.SendAsync(attemptId, payload.TenantId, payload.EmailAddress, token!, ct).ConfigureAwait(false);
            return (TenantInvitationDeliveryAttempt.Accepted, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Provider failures may carry recipient addresses or secrets; only the stable code is kept.
            return (TenantInvitationDeliveryAttempt.Rejected, DeliveryFailed);
        }
    }
}
