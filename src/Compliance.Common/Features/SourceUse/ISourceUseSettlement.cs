using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     The receiver command that settles one use of a source owner by its exact key. A settlement before any grant
///     fences the key against late acquisition.
/// </summary>
public interface ISourceUseSettlement : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>Deterministic command identity chosen by the process driver.</summary>
    Uuid CommandId { get; }

    /// <summary>The accepted source fact that started the process.</summary>
    NativeFactReference Source { get; }

    /// <summary>The source owner stream the use is settled on.</summary>
    EventStreamAddress Target { get; }

    /// <summary>The root operation id.</summary>
    Uuid OperationId { get; }

    /// <summary>The frozen source authorization generation.</summary>
    long Generation { get; }

    /// <summary>The expected revision of the target, when the receiver checks one.</summary>
    long? ExpectedRevision { get; }

    /// <summary>The workflow intent referenced by the proven source acceptance.</summary>
    ImmutableContentRef Intent { get; }

    /// <summary>The resource id of the source owner.</summary>
    Uuid SourceOwnerId { get; }

    /// <summary>How the use ends; it must match the verified terminal source event kind.</summary>
    SourceUseSettlementDisposition Disposition { get; }
}
