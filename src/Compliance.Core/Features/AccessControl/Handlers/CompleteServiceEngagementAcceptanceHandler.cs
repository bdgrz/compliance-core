using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Records the accepted engagement after the irreversible authorization. The snapshot is derived from the frozen
///     evidence at the server-frozen decision instant, never from anything read since, and the engagement revision
///     advances exactly once. The completion can never precede the authorization and always finishes forward.
/// </summary>
sealed class CompleteServiceEngagementAcceptanceHandler(EngagementCompletion completion)
    : IRequestHandler<CompleteServiceEngagementAcceptance, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteServiceEngagementAcceptance> context, CancellationToken ct)
    {
        var command = context.Request;
        var loaded = await completion.PrepareAsync(command.Source, command.Intent, command.OperationId,
            command.CommandId, command.Target, command.Generation, command.EngagementId, command.ServiceEpoch,
            command.CommitmentProofManifest, EngagementIndependenceOperationKind.Acceptance, ct)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, operation, intent) = (loaded.Owner!, loaded.Operation!, loaded.Intent!);
        var evidence = await completion.ReadEvidenceAsync(intent, ct).ConfigureAwait(false);
        var view = EngagementAcceptanceViews.Accepted(evidence, intent.Header.OriginalActor,
            operation.Authorized!.DecisionAt);
        var viewReference = await completion.WriteAsync(owner.TenantId, owner.Stream, command.OperationId,
            EngagementAcceptanceEffects.AcceptanceSchema, view, ct).ConfigureAwait(false);
        var result = await completion.WriteResultAsync(owner.TenantId, owner.Stream, command.OperationId, viewReference, ct)
            .ConfigureAwait(false);
        return await completion.AppendAsync(loaded, command.CommandId, nameof(CompleteServiceEngagementAcceptance),
            command.Source, context, (fresh, provenance) => fresh.RecordAcceptance(provenance, viewReference, result),
            ct).ConfigureAwait(false);
    }
}
