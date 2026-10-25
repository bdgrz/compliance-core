using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Ends a pending closure that can definitely never close the finding: the closure is canceled first, which fences
///     the delayed completion against late delivery, and only then is every action use settled as canceled, including the
///     uses that were never acquired, so a delayed acquisition is refused. All effects use the deterministic command ids
///     the process driver would use, so a later driver step replays them. A closure that already closed the finding
///     recovers forward. Nothing is inferred from missing native state.
/// </summary>
sealed class FindingClosureFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAsync(FindingClosure closure, NativeFactReference source,
        ImmutableContentRef intentReference, string reason, Uuid? actionId, IExecutionContext context, CancellationToken ct)
    {
        var (finding, intent, decision) = (closure.Finding!, closure.Intent!, closure.Decision!);
        var episode = intent.Header.Episode;
        var proof = finding.TerminalClosure(intent.Header.RootOperationId) is FindingClosureCanceled canceledClosure
            ? canceledClosure.SourceProofs ?? throw new ImmutableContentIntegrityException("The cancellation has no exact failure proof.")
            : await FindingContent.WriteAsync(content, closure.TenantId, FindingContent.FailureSchema, new FindingClosureFailureProof(decision.ProgramId, finding.FindingId, reason,
                actionId, source, intentReference, intent.Header.RootOperationId, (long)intent.ParentGeneration), ct).ConfigureAwait(false);
        var abortId = FindingClosureEffects.ReceiverCommandId(source.EventId, nameof(AbortFindingClosure), null, episode);
        var aborted = await bus.SendAsync(new AbortFindingClosure(abortId, intent.Header.RootOperationId, finding.Stream,
            (long)intent.ParentGeneration, 0, proof), context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        // A closure that already closed the finding recovers forward: its own settlement releases the uses.
        if (aborted.Value is not (ReceiverOutcome.Applied or ReceiverOutcome.Replayed))
            return aborted;
        var canceled = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => throw new InvalidOperationException("An abort that was not applied is handled above."),
        };
        var settled = await SettleCanceledAsync(closure, canceled, source, intentReference, context, ct).ConfigureAwait(false);
        return settled.IsSuccess ? SourceUseReceiverSupport.Superseded(reason, canceled) : settled;
    }

    /// <summary>Settles every action use of a canceled closure as canceled, in canonical key order.</summary>
    public async ValueTask<Result<ReceiverOutcome>> SettleCanceledAsync(FindingClosure closure, NativeFactReference canceled,
        NativeFactReference source, ImmutableContentRef intentReference, IExecutionContext context, CancellationToken ct)
    {
        var (finding, intent, decision) = (closure.Finding!, closure.Intent!, closure.Decision!);
        var tenant = closure.TenantId;
        IReadOnlyList<AdmittedChild> children;
        try
        {
            children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, tenant.ToString(),
                intent.AdmittedChildren, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The admitted action manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        foreach (var child in children.OrderBy(item => FindingClosureEffects.ActionKey(tenant, decision.ProgramId,
                     finding.FindingId, item.ChildId), Kernel.StreamKeyOrder.Instance))
        {
            var action = new CorrectiveAction(tenant, decision.ProgramId, finding.FindingId, child.ChildId);
            var key = FindingClosureEffects.ActionKey(tenant, decision.ProgramId, finding.FindingId, child.ChildId);
            var settlementId = FindingClosureEffects.ReceiverCommandId(source.EventId,
                nameof(SettleCorrectiveActionFindingClosureUse), key, intent.Header.Episode);
            var settled = await bus.SendAsync(new SettleCorrectiveActionFindingClosureUse(settlementId, canceled,
                action.Stream, intent.Header.RootOperationId, (long)intent.ParentGeneration, null, intentReference,
                child.ChildId, finding.FindingId, decision.ProgramId, SourceUseSettlementDisposition.Canceled), context, ct)
                .ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        return SourceUseReceiverSupport.Applied(canceled);
    }
}
