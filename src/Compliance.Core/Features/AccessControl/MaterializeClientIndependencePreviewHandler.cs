using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Materializes the exact historical preview, resolving native receipt knowledge before frozen payload reads.</summary>
sealed class MaterializeClientIndependencePreviewHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeClientIndependencePreview, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeClientIndependencePreview> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.EvaluationId == Uuid.Empty || command.Source is null || command.Intent is null ||
            command.OperationId != command.EvaluationId || command.Generation != ClientIndependencePreviewEffects.Generation ||
            command.ExpectedRevision is not (null or 0) || !Uuid.TryParse(command.Source.Stream.Realm, out var tenantId) || tenantId == Uuid.Empty)
            return SourceUseReceiverSupport.Permanent("The preview names another generation, operation or tenant.");
        var realm = IndependenceContent.Realm(tenantId);
        var decisionStream = new ClientIndependenceDecision(tenantId, command.EvaluationId).Stream;
        if (command.Target != decisionStream)
            return SourceUseReceiverSupport.Permanent("The preview names another decision stream.");
        var owner = await reader.HydrateAsync(new ClientServiceIndependence(tenantId, command.CommandId, command.EvaluationId), ct)
            .ConfigureAwait(false);
        if (owner.AdmissionOf(command.EvaluationId) is not { } admitted)
            return SourceUseReceiverSupport.Transient("The admitted evaluation is not visible yet.");
        if (command.Source.Stream != owner.Stream || admitted.Metadata.EventId != command.Source.EventId ||
            admitted.Metadata.AggregateVersion != command.Source.AggregateVersion || admitted.Intent != command.Intent)
            return SourceUseReceiverSupport.Permanent("The source is not the exact native admission of this evaluation.");

        var root = admitted.Operation;
        var header = new DurableIntentHeader(root.RootOperationId, root.FirstOwner ?? root.Owner, root.AffectedOwner,
            root.ExpectedRevisionOwner, root.Actor, root.Episode, root.OriginalExpectedRevision, root.AuthoredIntentSha256, root.AcceptedAt);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeClientIndependencePreview),
            admitted.Metadata.EventId, header, decisionStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Decide(ClientIndependenceDecision decision)
        {
            switch (decision.Acceptance.Decide(binding))
            {
                case NativeAcceptanceDecision.Replayed:
                    return SourceUseReceiverSupport.Replayed(Fact(decision.Stream, decision.Acceptance.SelectedFact!));
                case NativeAcceptanceDecision.Conflict:
                    return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
            }
            if (decision.Evaluated is not { } existing)
                return null;
            return existing.Intent == command.Intent && existing.Operation.RootOperationId == root.RootOperationId &&
                   existing.Operation.CausationEventId == admitted.Metadata.EventId && existing.Operation.Actor == root.Actor &&
                   existing.Operation.AuthoredIntentSha256 == root.AuthoredIntentSha256 &&
                   (existing.Operation.FirstOwner ?? root.Owner) == root.Owner
                ? SourceUseReceiverSupport.Replayed(Fact(decision.Stream, existing))
                : Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        var prepared = await NativeReceiverPreparation.PrepareAsync<ClientIndependenceDecision, Candidate>(reader,
            () => new ClientIndependenceDecision(tenantId, command.EvaluationId, command.CommandId), Decide,
            async (_, token) =>
            {
                try
                {
                    var intent = await content.ReadJsonAsync<ClientIndependencePreviewIntent>(realm, command.Intent, token).ConfigureAwait(false);
                    if (command.Intent.Schema != ClientIndependencePreviewIntent.Schema || intent.Header is null ||
                        intent.CompleteServices is null || intent.RuleContent is null || intent.SourceCuts is null || intent.HistoricalPracticeFacts is null ||
                        intent.CompleteServices.ItemSchema != ClientIndependencePreviewIntent.ServiceItemSchema ||
                        intent.CompleteServices.Order != SelectionOrder.Authored || intent.RuleContent.Schema != ClientIndependencePreviewIntent.RuleSchema ||
                        intent.SourceCuts.ItemSchema != ClientIndependencePreviewIntent.CutItemSchema || intent.SourceCuts.Order != SelectionOrder.CanonicalKey ||
                        intent.HistoricalPracticeFacts.ItemSchema != ClientIndependencePreviewIntent.PracticeItemSchema ||
                        intent.HistoricalPracticeFacts.Order != SelectionOrder.CanonicalKey)
                        return Result<Candidate>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The preview does not carry its exact complete selected snapshot schemas."));
                    var cuts = await content.ReadSelectionAsync<AuthoritativeReadCut>(realm, intent.SourceCuts, token).ConfigureAwait(false);
                    var practice = await content.ReadSelectionAsync<SourceFactLocator>(realm, intent.HistoricalPracticeFacts, token).ConfigureAwait(false);
                    var catalog = new IndependenceRuleCatalog().Stream;
                    var clientAnchor = $"{owner.Stream.Realm}/{owner.Stream.Area}/{owner.Stream.Resource}";
                    var ruleAnchor = $"{catalog.Realm}/{catalog.Area}/{catalog.Resource}";
                    if (cuts.Count != 2 || practice.Count != 0 || cuts.Any(cut => cut is null || cut.CapturedAt == default) ||
                        cuts.Count(cut => cut.AnchorSelector == clientAnchor) != 1 || cuts.Count(cut => cut.AnchorSelector == ruleAnchor) != 1 ||
                        cuts.Single(cut => cut.AnchorSelector == ruleAnchor).GlobalWatermarkExclusive == 0 ||
                        intent.ServiceEpoch != 0 && cuts.Single(cut => cut.AnchorSelector == clientAnchor).GlobalWatermarkExclusive == 0)
                        return Result<Candidate>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The preview has another set of independently anchored complete source frontiers."));
                    var services = await content.ReadSelectionAsync<NonattestServiceView>(realm, intent.CompleteServices, token).ConfigureAwait(false);
                    var rules = await content.ReadJsonAsync<IndependenceRuleVersionView>(realm, intent.RuleContent, token).ConfigureAwait(false);
                    if (intent.Header != header || intent.EvaluationId != command.EvaluationId || intent.ClientTenantId != tenantId ||
                        intent.ServiceEpoch != (ulong)admitted.Revision || (ulong)services.Count != intent.ServiceEpoch ||
                        services.Any(service => service is null || service.ClientTenantId != tenantId || !IndependenceRecordValidation.ValidService(service.Content)) ||
                        intent.RuleVersionDigest != IndependenceSourceDigest.RuleContent(rules.Content) || intent.RuleSetVersion != rules.Version ||
                        AuthoredIntentDigest.Compute(new EvaluateClientIndependence(tenantId, command.EvaluationId,
                            intent.RuleSetVersion, intent.ExaminationPeriodStart, admitted.Revision)) != root.AuthoredIntentSha256)
                        return Result<Candidate>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The frozen preview differs from its exact original native request and scope."));
                    var evaluated = ClientIndependencePreviewEvaluator.Evaluate(tenantId, command.EvaluationId, rules,
                        intent.ExaminationPeriodStart, services, header.OriginalActor, header.AcceptedAt);
                    if (!evaluated.IsSuccess)
                        return Result<Candidate>.Failure(evaluated.Error!);
                    var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(command.CommandId, "preview-content"),
                        IndependenceContent.EvaluationSchema, evaluated.Value!, token).ConfigureAwait(false);
                    var result = await IndependenceContent.WriteResultAsync(content, realm, IndependenceContent.EvaluationSchema, body, token)
                        .ConfigureAwait(false);
                    return Result<Candidate>.Success(new Candidate(body, result));
                }
                catch (ImmutableContentIntegrityException error)
                {
                    return Result<Candidate>.Failure(IndependenceCatalogContent.Failure(error));
                }
            }, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ReceiverOutcome>.Failure(prepared.Error!);
        if (prepared.Value!.Outcome is { } known)
            return known;
        var candidate = prepared.Value.Value!;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(new ClientIndependenceDecision(tenantId, command.EvaluationId, command.CommandId), decision =>
                {
                    if (Decide(decision) is { } resolved)
                        return AggregateOutcome.Discard(resolved);
                    var applied = decision.Materialize(provenance, candidate.Content, command.Intent, candidate.Result);
                    return applied.IsSuccess
                        ? AggregateOutcome.Commit(SourceUseReceiverSupport.Applied(Fact(decision.Stream, decision.Acceptance.SelectedFact!)))
                        : AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(applied.Error!));
                }, context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // An equal materializer may have committed; the next attempt decides its original native receipt.
            }
        }
    }

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);
    sealed record Candidate(Proofs.ImmutableContentRef Content, Proofs.ImmutableContentRef Result);
}
