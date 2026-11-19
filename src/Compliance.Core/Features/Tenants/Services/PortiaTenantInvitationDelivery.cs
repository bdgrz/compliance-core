using Cntryl.Portia;
using Cntryl.Portia.Communications;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;

namespace Bdgrz.Compliance.Features.Tenants.Services;

public sealed class PortiaTenantInvitationDelivery(IMailSender sender) : ITenantInvitationDelivery
{
    public async ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress,
        string token, CancellationToken ct)
    {
        var message = new MailMessage([emailAddress], "Your Badgers organization invitation",
            $"Sign in and verify this email address to accept the invitation for organization {tenantId}. " +
            $"Your invitation code is {token}. It expires in 7 days.\n");
        SendOutcome outcome;
        try
        {
            outcome = await sender.SendAsync(new TenantId(tenantId.ToString()),
                new SendId($"invitation-{attemptId}"), message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (SendThrottledException)
        {
            throw Failure(TenantInvitationDeliveryAttempt.Rejected, "provider_throttled");
        }
        catch (InvalidOperationException)
        {
            throw Failure(TenantInvitationDeliveryAttempt.Rejected, "provider_misconfigured");
        }
        catch (Exception)
        {
            throw Failure(TenantInvitationDeliveryAttempt.Unknown, "transport_ambiguous");
        }

        switch (outcome.Status)
        {
            case SendStatus.Accepted:
                return;
            case SendStatus.Rejected:
                throw Failure(TenantInvitationDeliveryAttempt.Rejected, "provider_rejected");
            case SendStatus.Unknown:
                throw Failure(TenantInvitationDeliveryAttempt.Unknown, "provider_outcome_unknown");
            default:
                throw new InvalidOperationException("The mail sender returned an unsupported outcome.");
        }
    }

    static TenantInvitationDeliveryException Failure(string result, string code) => new(result, code);
}
