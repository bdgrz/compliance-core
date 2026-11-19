using Cntryl.Portia;
using Cntryl.Portia.Communications;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;

namespace Bdgrz.Compliance.Features.UserIdentities.Services;

public sealed class PortiaEmailChallengeDelivery(IMailSender sender) : IEmailChallengeDelivery
{
    readonly IMailSender _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    static readonly TenantId IdentityTenant = new("identity");

    public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
        CancellationToken ct) => SendAsync("verification", challengeId, emailAddress,
        "Verify your Badgers email address",
        $"Your Badgers email verification code is {token}. It expires in 15 minutes.\n", ct);

    public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
        CancellationToken ct) => SendAsync("recovery", challengeId, emailAddress,
        "Secure your Badgers account",
        $"Your account identity recovery code is {token}. It expires in 15 minutes. " +
        "If you did not request this code, you can ignore this message.\n", ct);

    public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId, string emailAddress,
        CancellationToken ct) => SendAsync("recovery-completed", challengeId, emailAddress,
        "Your Badgers account identity was updated",
        "An identity linked to your Badgers account was replaced. If you did not make this " +
        "change, contact support and secure your email account.\n", ct);

    async ValueTask SendAsync(string purpose, Uuid messageId, string recipient, string subject,
        string body, CancellationToken ct)
    {
        var message = new MailMessage([recipient], subject, body);
        SendOutcome outcome;
        try
        {
            outcome = await _sender.SendAsync(IdentityTenant,
                new SendId($"{purpose}-{messageId}"), message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (SendThrottledException)
        {
            throw Failure(EmailChallengeFailureCertainty.DefiniteTransient, "provider_throttled");
        }
        catch (InvalidOperationException)
        {
            throw Failure(EmailChallengeFailureCertainty.DefinitePermanent, "provider_misconfigured");
        }
        catch (Exception)
        {
            throw Failure(EmailChallengeFailureCertainty.Unknown, "transport_ambiguous");
        }

        switch (outcome.Status)
        {
            case SendStatus.Accepted:
                return;
            case SendStatus.Rejected:
                throw Failure(EmailChallengeFailureCertainty.DefinitePermanent, "provider_rejected");
            case SendStatus.Unknown:
                throw Failure(EmailChallengeFailureCertainty.Unknown, "provider_outcome_unknown");
            default:
                throw new InvalidOperationException("The mail sender returned an unsupported outcome.");
        }
    }

    static EmailChallengeDeliveryException Failure(string certainty, string code) => new(certainty, code);
}
