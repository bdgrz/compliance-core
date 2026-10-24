using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Applies one frozen target group under its hold as one preflighted native append batch: a provisional
///     <see cref="ApplicationImportEffectPending" /> for every frozen row in original row order. The baseline is
///     compared once; a fence recorded on the target wins over a delayed apply.
/// </summary>
[Discriminator("bdgrz.declared_application.apply_import_effect", 2)]
public sealed record ApplyApplicationImportEffect(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid BatchId, Uuid ApplicationId, ImmutableContentRef RowGroup)
    : IRequest<ReceiverOutcome>, IApplicationImportReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
