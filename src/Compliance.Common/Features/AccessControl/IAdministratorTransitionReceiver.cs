using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>An internal receiver command of the administrator transition process; only the system principal may send it.</summary>
public interface IAdministratorTransitionReceiver : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>The deterministic command id the process driver chose for this receiver step.</summary>
    Uuid CommandId { get; }

    /// <summary>The native stream the receiver mutates.</summary>
    EventStreamAddress Target { get; }

    /// <summary>The root operation of the guard admission this step belongs to.</summary>
    Uuid OperationId { get; }

    /// <summary>The guard generation of the fact that admitted the root operation.</summary>
    long Generation { get; }

    /// <summary>The authored revision of the source owner; null when the receiver does not constrain it.</summary>
    long? ExpectedRevision { get; }

    /// <summary>The frozen administrator transition intent.</summary>
    ImmutableContentRef Intent { get; }

    /// <summary>The member the transition concerns.</summary>
    Uuid MemberId { get; }
}
