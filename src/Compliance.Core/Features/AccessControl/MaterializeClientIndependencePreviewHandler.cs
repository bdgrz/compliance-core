using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Materializes the immutable historical preview of an admitted evaluation. The admission is found on the
///     authoritative service-epoch owner and the frozen intent is read from verified content; the preview is computed
///     from that snapshot only, never from a live source, and always stays blocked from production acceptance. An exact
///     replay returns the original fact; an intent that differs from the admitted one is a permanent conflict.
/// </summary>
sealed class MaterializeClientIndependencePreviewHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeClientIndependencePreview, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeClientIndependencePreview> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.EvaluationId == Uuid.Empty ||
            command.OperationId != command.EvaluationId || command.Generation != ClientIndependencePreviewEffects.Generation ||
            !Uuid.TryParse(command.Source.Stream.Realm, out var tenantId) || tenantId == Uuid.Empty)
            return SourceUseReceiverSupport.Permanent("The preview names another generation, operation or tenant.");
        var realm = IndependenceContent.Realm(tenantId);
        var decisionStream = new ClientIndependenceDecision(tenantId, command.EvaluationId).Stream;
        if (command.Target != decisionStream)
            return SourceUseReceiverSupport.Permanent("The preview names another decision stream.");

        var owner = await reader.HydrateAsync(new ClientServiceIndependence(tenantId), ct).ConfigureAwait(false);
        if (owner.AdmissionOf(command.EvaluationId) is not { } admitted)
            return SourceUseReceiverSupport.Transient("The admitted evaluation is not visible yet.");
        if (command.Source.Stream != owner.Stream || admitted.Metadata.EventId != command.Source.EventId ||
            admitted.Metadata.AggregateVersion != command.Source.AggregateVersion || admitted.Intent != command.Intent)
            return SourceUseReceiverSupport.Permanent("The source is not the admission of this evaluation.");

        ClientIndependencePreviewIntent intent;
        IReadOnlyList<NonattestServiceView> services;
        IndependenceRuleVersionView rules;
        try
        {
            intent = await content.ReadJsonAsync<ClientIndependencePreviewIntent>(realm, command.Intent, ct)
                .ConfigureAwait(false);
            services = await content.ReadSelectionAsync<NonattestServiceView>(realm, intent.CompleteServices, ct)
                .ConfigureAwait(false);
            rules = await content.ReadJsonAsync<IndependenceRuleVersionView>(realm, intent.RuleContent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen preview cannot be verified: {exception.Message}");
        }

        if (intent.EvaluationId != command.EvaluationId || intent.ClientTenantId != tenantId ||
            intent.Header.RootOperationId != command.EvaluationId ||
            intent.ServiceEpoch != (ulong)admitted.Revision || services.Count != (int)intent.ServiceEpoch ||
            intent.RuleVersionDigest != IndependenceSourceDigest.RuleContent(rules.Content) ||
            intent.RuleSetVersion != rules.Version)
            return SourceUseReceiverSupport.Permanent("The command differs from the frozen preview intent.");

        var evaluated = ClientIndependencePreviewEvaluator.Evaluate(tenantId, command.EvaluationId, rules,
            intent.ExaminationPeriodStart, services, intent.Header.OriginalActor, intent.Header.AcceptedAt);
        if (!evaluated.IsSuccess)
            return SourceUseReceiverSupport.Permanent(evaluated.Error!.Message);
        var evaluationContent = await content.WriteJsonAsync(realm,
            Uuid.CreateVersion5(command.CommandId, "preview-content"), IndependenceContent.EvaluationSchema,
            evaluated.Value!, ct).ConfigureAwait(false);
        var result = await IndependenceContent.WriteResultAsync(content, realm, IndependenceContent.EvaluationSchema,
            evaluationContent, ct).ConfigureAwait(false);

        var provenance = ClientIndependencePreviewEffects.Child(command.CommandId,
            nameof(MaterializeClientIndependencePreview), command.Source.EventId, intent, decisionStream,
            clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ClientIndependenceDecision(tenantId, command.EvaluationId,
                    command.CommandId), decision =>
                    {
                        switch (decision.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(decision.Stream, decision.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (decision.Evaluated is { } existing)
                            return AggregateOutcome.Discard(existing.Intent == command.Intent
                                ? Result<Step>.Success(new Step(Fact(decision.Stream, existing), false))
                                : Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        var materialized = decision.Materialize(provenance, evaluationContent, command.Intent, result);
                        return materialized.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(decision.Stream, decision.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(materialized.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery of the same preview won the append; rehydrate and decide again.
            }
        }
    }

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    sealed record Step(NativeFactReference Fact, bool Applied);
}
