using Cntryl.Portia;
using Cntryl.Portia.Communications;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;

namespace Bdgrz.Compliance.Features.Work.Services;

sealed class PortiaWorkDigestDelivery(IMailSender sender) : IWorkDigestDelivery
{
    public async ValueTask<WorkDigestTransportOutcome> SendAsync(Uuid tenantId,
        WorkDigestDeliveryMessage digest, CancellationToken ct)
    {
        var message = new MailMessage([digest.Recipient], digest.Subject, digest.TextBody);
        SendOutcome outcome;
        try
        {
            outcome = await sender.SendAsync(new TenantId(tenantId.ToString()),
                new SendId($"work-digest-{digest.MessageId}"), message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (SendThrottledException)
        {
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.DefiniteTransientRejection,
                "provider_throttled");
        }
        catch (InvalidOperationException)
        {
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.DefinitePermanentRejection,
                "provider_misconfigured");
        }
        catch (Exception)
        {
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Unknown,
                "transport_ambiguous");
        }

        return outcome.Status switch
        {
            SendStatus.Accepted => new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Accepted),
            SendStatus.Rejected => new WorkDigestTransportOutcome(
                WorkDigestTransportOutcomeKind.DefinitePermanentRejection, "provider_rejected"),
            SendStatus.Unknown => new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Unknown,
                "provider_outcome_unknown"),
            _ => throw new InvalidOperationException("The mail sender returned an unsupported outcome."),
        };
    }
}
