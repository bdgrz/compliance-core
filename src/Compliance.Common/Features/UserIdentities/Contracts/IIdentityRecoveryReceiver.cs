using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

/// <summary>An internal receiver command of the identity recovery process; only the system principal may send it.</summary>
public interface IIdentityRecoveryReceiver : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>The deterministic command id the process driver chose for this receiver step.</summary>
    Uuid CommandId { get; }

    /// <summary>The recovery challenge, which is the root operation of the process.</summary>
    Uuid ChallengeId { get; }
}
