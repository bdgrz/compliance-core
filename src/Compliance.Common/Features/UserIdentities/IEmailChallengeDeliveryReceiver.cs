using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>An internal receiver command of the email challenge delivery process; only the system principal may send it.</summary>
public interface IEmailChallengeDeliveryReceiver : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>The deterministic command id the process driver chose for this receiver step.</summary>
    Uuid CommandId { get; }

    /// <summary>The challenge being delivered, which is the root operation of the process.</summary>
    Uuid ChallengeId { get; }

    /// <summary>
    ///     The accepted challenge fact on the address stream. The receiver verifies it by hydrating the address: the
    ///     stream, event id and owner revision must be those of the address's current challenge fact.
    /// </summary>
    NativeFactReference Source { get; }

    /// <summary>The technical delivery owner of the challenge.</summary>
    EventStreamAddress Target { get; }

    /// <summary>The root operation of the process, which equals the challenge.</summary>
    Uuid OperationId { get; }

    /// <summary>The generation of the source fact; one challenge freezes exactly one.</summary>
    long Generation { get; }

    /// <summary>The authored revision of the target; null because delivery does not constrain it.</summary>
    long? ExpectedRevision { get; }

    /// <summary>The frozen delivery intent.</summary>
    ImmutableContentRef Intent { get; }
}
