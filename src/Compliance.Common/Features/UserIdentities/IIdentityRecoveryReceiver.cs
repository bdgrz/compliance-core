using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>An internal receiver command of the identity recovery process; only the system principal may send it.</summary>
public interface IIdentityRecoveryReceiver : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>The deterministic command id the process driver chose for this receiver step.</summary>
    Uuid CommandId { get; }

    /// <summary>The recovery challenge, which is the root operation of the process.</summary>
    Uuid ChallengeId { get; }
}
