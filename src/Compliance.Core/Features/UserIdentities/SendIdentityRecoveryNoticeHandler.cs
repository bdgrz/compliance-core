using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Sends the completion notice to the verified address under the at-least-once notice policy. The swap is already
///     durable, so a failing delivery is a transient failure of this receiver only: it retries until it succeeds and
///     never changes the completed recovery.
/// </summary>
public sealed class SendIdentityRecoveryNoticeHandler(IdentityRecoveryReceiverSupport support,
    IEmailChallengeDelivery delivery) : IRequestHandler<SendIdentityRecoveryNotice, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<SendIdentityRecoveryNotice> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        if (request.Target != IdentityRecoveryReceiverSupport.NoticeStream(request.ChallengeId) ||
            !EmailAddresses.TryNormalize(request.Recipient, out var recipient))
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not name this recovery's notice and a valid recipient.");

        var address = await support.Reader.HydrateAsync(new EmailAddress(recipient, Uuid.Empty, null, request.ChallengeId), ct).ConfigureAwait(false);
        if (address.ClaimOf(request.ChallengeId) is not { } claim || claim.Fact.UserId != intent.Value!.AccountId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The recipient is not the address that claimed this recovery.");
        if (!address.IsRecoveryChallengeCompleted(request.ChallengeId))
            return IdentityRecoveryReceiverSupport.Wait("The original challenge is not completed yet.");

        try
        {
            await delivery.SendRecoveryCompletedAsync(request.ChallengeId, claim.Fact.UserId, recipient, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The notice is independent of the completed recovery; its own record retries it.
            return IdentityRecoveryReceiverSupport.Wait("Identity recovery notice delivery failed.");
        }

        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Performed("recovery notice sent"));
    }
}
