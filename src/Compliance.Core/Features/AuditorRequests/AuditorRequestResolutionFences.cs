using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Ends a pending resolution that can definitely never resolve the request: the resolution is canceled first, which
///     fences the delayed completion against late delivery, and only then is every response use settled as canceled,
///     including the uses that were never acquired, so a delayed acquisition is refused. All effects use the deterministic
///     command ids the process driver would use, so a later driver step replays them. A resolution that already resolved
///     the request recovers forward. Nothing is inferred from missing native state.
/// </summary>
sealed class AuditorRequestResolutionFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAsync(AuditorRequestResolution resolution,
        NativeFactReference source, ImmutableContentRef intentReference, string reason, Uuid? responseId,
        IExecutionContext context, CancellationToken ct)
    {
        var (request, intent, decision) = (resolution.Request!, resolution.Intent!, resolution.Decision!);
        var episode = intent.Header.Episode;
        var proof = await AuditorContent.WriteAsync(content, resolution.TenantId, AuditorContent.FailureSchema,
            new AuditorRequestFailureProof(decision.ProgramId, request.RequestId, reason, responseId), ct)
            .ConfigureAwait(false);
        var abortId = AuditorRequestResolutionEffects.ReceiverCommandId(source.EventId,
            nameof(AbortAuditorRequestResolution), null, episode);
        var aborted = await bus.SendAsync(new AbortAuditorRequestResolution(abortId, intent.Header.RootOperationId,
            request.Stream, (long)intent.ParentGeneration, 0, proof), context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        // A resolution that already resolved the request recovers forward: its own settlement releases the uses.
        if (aborted.Value is not (ReceiverOutcome.Applied or ReceiverOutcome.Replayed))
            return aborted;
        var canceled = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => throw new InvalidOperationException("An abort that was not applied is handled above."),
        };
        var settled = await SettleCanceledAsync(resolution, canceled, source, intentReference, context, ct)
            .ConfigureAwait(false);
        return settled.IsSuccess ? SourceUseReceiverSupport.Superseded(reason, canceled) : settled;
    }

    /// <summary>Settles every response use of a canceled resolution as canceled, in canonical key order.</summary>
    public async ValueTask<Result<ReceiverOutcome>> SettleCanceledAsync(AuditorRequestResolution resolution,
        NativeFactReference canceled, NativeFactReference source, ImmutableContentRef intentReference,
        IExecutionContext context, CancellationToken ct)
    {
        var (request, intent, decision) = (resolution.Request!, resolution.Intent!, resolution.Decision!);
        var tenant = resolution.TenantId;
        IReadOnlyList<AdmittedRequestChild> children;
        try
        {
            children = await AuditorRequestResolutionProofs.ChildrenAsync(content, tenant, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The admitted child manifest cannot be verified: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The admitted child manifest cannot be verified: {exception.Message}");
        }

        foreach (var child in children.Where(static child => child.Kind == AdmittedRequestChildKind.Response)
                     .OrderBy(item => AuditorRequestResolutionEffects.ResponseKey(tenant, decision.ProgramId,
                         request.RequestId, item.ChildId), Kernel.StreamKeyOrder.Instance))
        {
            var response = new AuditorResponse(tenant, decision.ProgramId, request.RequestId, child.ChildId);
            var key = AuditorRequestResolutionEffects.ResponseKey(tenant, decision.ProgramId, request.RequestId,
                child.ChildId);
            var settlementId = AuditorRequestResolutionEffects.ReceiverCommandId(source.EventId,
                nameof(SettleAuditorResponseRequestResolutionUse), key, intent.Header.Episode);
            var settled = await bus.SendAsync(new SettleAuditorResponseRequestResolutionUse(settlementId, canceled,
                response.Stream, intent.Header.RootOperationId, (long)intent.ParentGeneration, null, intentReference,
                child.ChildId, request.RequestId, decision.ProgramId, SourceUseSettlementDisposition.Canceled), context, ct)
                .ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        return SourceUseReceiverSupport.Applied(canceled);
    }
}
