using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Materializes the immutable link from its admission. The admission is verified from the gap owner: the exact
///     fact, the frozen intent and the admitted revision. The gap is never changed, so a gap that closed after the
///     admission still materializes the admitted historical link, and the link keeps the admission revision and
///     instant. An exact retry replays the original acceptance.
/// </summary>
sealed class MaterializeProviderRiskAcceptanceLinkHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeProviderRiskAcceptanceLink, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeProviderRiskAcceptanceLink> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationId == Uuid.Empty || request.LinkId != request.OperationId ||
            request.GapId == Uuid.Empty || !Uuid.TryParse(request.Source.Stream.Realm, out var tenantId) ||
            tenantId == Uuid.Empty || request.Target != ProviderAssuranceStreams.RiskAcceptance(tenantId, request.LinkId))
            return SourceUseReceiverSupport.Permanent("The materialization names another link, gap or tenant.");
        ProviderRiskLinkIntent intent;
        try
        {
            intent = await ProviderAssuranceContent.ReadAsync<ProviderRiskLinkIntent>(content, tenantId, request.Intent,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen link cannot be verified: {exception.Message}");
        }
        if (intent.LinkId != request.LinkId || intent.GapId != request.GapId ||
            intent.Header.RootOperationId != request.OperationId ||
            (long)intent.GapAdmissionRevision != request.Generation)
            return SourceUseReceiverSupport.Permanent("The command differs from the frozen link intent.");
        var gap = await reader.HydrateAsync(new ProviderAssuranceGap(tenantId, intent.ProviderId, intent.GapId), ct)
            .ConfigureAwait(false);
        if (gap.AdmissionOf(request.LinkId) is not { } admitted)
            return SourceUseReceiverSupport.Transient("The admitted link is not visible yet.");
        if (request.Source.Stream != gap.Stream || admitted.Metadata.EventId != request.Source.EventId ||
            admitted.Metadata.AggregateVersion != request.Source.AggregateVersion ||
            admitted.Intent != request.Intent || admitted.Revision != request.Generation ||
            intent.Header.FirstOwner != gap.Stream)
            return SourceUseReceiverSupport.Permanent("The source is not the admission of this link.");
        var provenance = new ProviderRiskAcceptance(tenantId, request.LinkId).Provenance(request.CommandId,
            nameof(MaterializeProviderRiskAcceptanceLink), request.Source.EventId, intent.Header, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var view = new ProviderCoverageGapRiskAcceptanceView(intent.ProgramId, intent.RiskId, intent.AcceptanceId,
            intent.AcceptanceExpiresAt, intent.Header.OriginalActor, intent.LinkedAt)
        {
            Revision = admitted.Revision,
        };
        var acceptance = await ProviderAssuranceContent.WriteAsync(content, tenantId, ProviderAssuranceContent.LinkSchema,
            view, ct, Uuid.CreateVersion5(request.CommandId, ProviderAssuranceContent.LinkSchema))
            .ConfigureAwait(false);
        var result = await ProviderAssuranceContent.WriteAsync(content, tenantId,
            ProviderAssuranceContent.GapRegistrationSchema,
            new ProviderCoverageGapRegistration(intent.GapId, admitted.Revision), ct,
            Uuid.CreateVersion5(request.CommandId, ProviderAssuranceContent.GapRegistrationSchema))
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var owner = await reader.HydrateAsync(new ProviderRiskAcceptance(tenantId, request.LinkId,
                    request.CommandId), ct).ConfigureAwait(false);
                if (Decide(owner, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new ProviderRiskAcceptance(tenantId, request.LinkId,
                    request.CommandId), fresh =>
                {
                    if (Decide(fresh, binding) is { } decided)
                        return AggregateOutcome.Discard(Result<Decision>.Success(new Decision(decided)));
                    var raised = fresh.Materialize(provenance, intent.GapId, intent.ProviderId, admitted.Revision,
                        acceptance, request.Intent, result);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Decision>.Success(new Decision(
                            SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                fresh.Acceptance.SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Decision>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The link changed concurrently; rehydrate and decide again.
            }
        }
    }

    static Result<ReceiverOutcome>? Decide(ProviderRiskAcceptance owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        return owner.Linked is { } linked
            ? SourceUseReceiverSupport.Superseded("The link is already materialized.",
                NativeFactReference.Of(owner.Stream, linked))
            : null;
    }

    sealed record Decision(Result<ReceiverOutcome> Outcome);
}
