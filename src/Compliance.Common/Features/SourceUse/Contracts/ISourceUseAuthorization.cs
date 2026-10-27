using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.SourceUse.Contracts;

/// <summary>
///     The receiver command that acquires one use of a source owner. <see cref="Intent" /> references the immutable
///     workflow intent of the proven source acceptance; the receiver derives the authorization intent from it.
/// </summary>
public interface ISourceUseAuthorization : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>Deterministic command identity chosen by the process driver.</summary>
    Uuid CommandId { get; }

    /// <summary>The accepted source fact that started the process.</summary>
    NativeFactReference Source { get; }

    /// <summary>The source owner stream the use is acquired on.</summary>
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

    /// <summary>The pinned content event of the source owner.</summary>
    Uuid SourceContentEventId { get; }

    /// <summary>The pinned content revision of the source owner.</summary>
    ulong ExpectedContentRevision { get; }
}
