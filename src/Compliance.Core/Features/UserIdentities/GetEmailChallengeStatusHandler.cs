using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Reads the status of the current verification challenge: the address says whether it is verified, issued or
///     expired, and the delivery process's ledger says whether the message was delivered, is pending or failed.
/// </summary>
public sealed class GetEmailChallengeStatusHandler(IAggregateReader reader, IEmailChallengeDeliveryLedger deliveries,
    TimeProvider clock) : IRequestHandler<GetEmailChallengeStatus, EmailChallengeStatusView>
{
    public async ValueTask<Result<EmailChallengeStatusView>> HandleAsync(
        IRequestContext<GetEmailChallengeStatus> context, CancellationToken ct)
    {
        if (!EmailAddresses.TryNormalize(context.Request.EmailAddress, out var normalized))
            return Result<EmailChallengeStatusView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a valid email address."));

        var address = await reader.HydrateAsync(new EmailAddress(normalized), ct).ConfigureAwait(false);
        if (address.Owner != context.Request.UserId)
            return Result<EmailChallengeStatusView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The email address was not found."));
        var delivery = address.CurrentChallengeId is { } challengeId
            ? await deliveries.ReadAsync(challengeId, ct).ConfigureAwait(false)
            : null;
        return Result<EmailChallengeStatusView>.Success(address.GetChallengeStatus(clock.GetUtcNow(),
            delivery?.Status ?? "pending"));
    }
}
