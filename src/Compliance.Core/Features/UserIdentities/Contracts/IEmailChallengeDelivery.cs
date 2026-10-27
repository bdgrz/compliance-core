using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

/// <summary>Delivery boundary for a verification challenge. The domain stores only a token hash.</summary>
public interface IEmailChallengeDelivery
{
    ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
        CancellationToken ct);

    ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
        CancellationToken ct);

    ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId, string emailAddress,
        CancellationToken ct);
}
