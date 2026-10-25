using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Materializes the immutable partner evaluation of an authorized personal decision. The evaluation is computed
///     from the frozen intent and the pinned source contents only, never from a live read: complete service history,
///     ratified rules, the partner's staff record and designation, and the engagement content revision the decision
///     pinned. It keeps the original authenticated actor and the frozen decision instant.
/// </summary>
sealed class MaterializePartnerIndependenceDecisionHandler(IAggregateExecutor executor,
    IImmutableContentStore content, IDomainEventReader events, EngagementPersonalDecisionVerifier verifier)
    : IRequestHandler<MaterializePartnerIndependenceDecision, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializePartnerIndependenceDecision> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.EvaluationId == Uuid.Empty ||
            command.OperationId != command.EvaluationId ||
            command.Generation != EngagementPersonalDecisionEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The evaluation names another operation or generation.");
        if (!Uuid.TryParse(command.Source.Stream.Resource, out var engagementId))
            return SourceUseReceiverSupport.Permanent("The source is not an engagement fact.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId,
            engagementId, command.CommandId, ct, DecisionSource.Authorization).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (engagement, decision, intent) = (loaded.Owner!, loaded.Decision!, loaded.Intent!);
        var tenantId = engagement.TenantId;
        var target = new PartnerIndependenceDecision(tenantId, command.EvaluationId).Stream;
        if (command.Target != target || intent.Kind != EngagementPersonalDecisionKind.PartnerEvaluation ||
            intent.AuthoredContent is not PartnerEvaluationContent authored)
            return SourceUseReceiverSupport.Permanent("The command differs from the frozen personal decision intent.");
        var authorized = decision.Authorized!;

        var realm = IndependenceContent.Realm(tenantId);
        var built = await BuildAsync(engagement, intent, authored, loaded.Requirements!, authorized.DecisionAt, realm, ct)
            .ConfigureAwait(false);
        if (!built.IsSuccess)
            return Result<ReceiverOutcome>.Failure(built.Error!);
        var (evaluation, rules) = built.Value;
        var evaluationContent = await IndependenceContent.WriteAsync(content, realm,
            EngagementPersonalDecisionEffects.EvaluationSchema, evaluation, ct).ConfigureAwait(false);
        var rulesContent = await IndependenceContent.WriteAsync(content, realm,
            EngagementPersonalDecisionEffects.RatifiedRulesSchema, rules, ct).ConfigureAwait(false);
        var result = await IndependenceContent.WriteResultAsync(content, realm,
            EngagementPersonalDecisionEffects.EvaluationSchema, evaluationContent, ct).ConfigureAwait(false);

        var provenance = EngagementPersonalDecisionEffects.Child(command.CommandId,
            nameof(MaterializePartnerIndependenceDecision), command.Source.EventId, intent, target,
            authorized.DecisionAt);
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PartnerIndependenceDecision(tenantId, command.EvaluationId,
                    command.CommandId), child =>
                    {
                        switch (child.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(child.Stream, child.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (child.Recorded is { } existing)
                            return AggregateOutcome.Discard(existing.Intent == command.Intent
                                ? Result<Step>.Success(new Step(NativeFactReference.Of(child.Stream, existing), false))
                                : Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        var materialized = child.Materialize(provenance, evaluationContent, rulesContent,
                            command.Intent, result);
                        return materialized.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(child.Stream, child.Acceptance.SelectedFact!), true)))
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
                // A concurrent delivery of the same evaluation won the append; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<(PartnerIndependenceEvaluationView Evaluation, IndependenceRuleVersionView Rules)>> BuildAsync(
        ServiceEngagementIndependence engagement, EngagementPersonalDecisionIntent intent,
        PartnerEvaluationContent authored, IReadOnlyList<SourceUseRequirement> requirements, DateTimeOffset decisionAt,
        string realm, CancellationToken ct)
    {
        try
        {
            if (EngagementPersonalDecisionEffects.RequirementOf(requirements, FirmStaffRecord.Area) is not { } staffUse ||
                EngagementPersonalDecisionEffects.RequirementOf(requirements, FirmProfessionalDutyDesignation.Area) is not
                { } dutyUse ||
                EngagementPersonalDecisionEffects.RequirementOf(requirements, IndependenceRuleCatalog.Area) is not
                { } rulesUse)
                return Failure("A partner evaluation requires its staff, designation and rule sources.");
            var services = await content.ReadSelectionAsync<NonattestServiceView>(realm, intent.CompleteServiceManifest, ct)
                .ConfigureAwait(false);
            var staff = await FirmStaffContent.ReadStaffAsync(content, staffUse.RequiredFacts, ct).ConfigureAwait(false);
            var duty = await ProfessionalDutyContent.ReadDesignationAsync(content, dutyUse.RequiredFacts, ct)
                .ConfigureAwait(false);
            var rules = await content.ReadJsonAsync<IndependenceRuleVersionView>(
                EngagementPersonalDecisionEffects.PlatformRealm, rulesUse.RequiredFacts, ct).ConfigureAwait(false);
            var history = await ServiceEngagementHistory.ReadAsync(events, engagement, ct).ConfigureAwait(false);
            var change = history.Changes.SingleOrDefault(item => item.Revision == intent.ExpectedEngagementRevision);
            if (change?.OperationResult is null || services.Count != (int)intent.ServiceEpoch)
                return Failure("The pinned engagement content or service history is not retained.");
            var view = await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm,
                change.OperationResult, ct).ConfigureAwait(false);
            var frozen = Array.AsReadOnly(services.Select(IndependenceRecordValidation.Freeze).ToArray());
            if (rules.Version != authored.RuleVersion ||
                IndependenceSourceDigest.RuleContent(rules.Content) != authored.ExpectedRuleContentDigest ||
                IndependenceSourceDigest.Services(services.ToArray()) != authored.ExpectedServiceHistoryDigest)
                return Failure("The pinned rules or service history differ from the authored evaluation.");

            var records = services.Select(service => new NonattestServiceRecord(engagement.TenantId,
                service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
                service.Content.EndedOn, service.Content.FirmStaffMemberIds,
                service.Content.InvolvedManagementFunctions)).ToArray();
            var policy = new IndependenceRuleSet(rules.Version, rules.Content.LookBackMonths,
                rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                    IndependenceWire.Classification(rule.Classification),
                    IndependenceWire.Classification(rule.ManagementFunctionsClassification))).ToArray());
            var decision = IndependenceCompartments.CanAcceptAttestEngagement(engagement.TenantId, policy, records,
                view.Content.PeriodStart, partnerEvaluationRecorded: false);
            if (decision.Code != IndependenceDecisionCode.PartnerEvaluationRequired ||
                decision.EvaluationOutcome != IndependenceEvaluationOutcome.ConditionallyCompatible)
                return Failure("Partner evaluation is available only for a fully classified conditionally compatible service history.");
            var considered = decision.ConsideredServices.Select(item => item.Service).ToHashSet();
            var evaluation = new PartnerIndependenceEvaluationView(intent.DecisionId, engagement.TenantId,
                intent.EngagementId, (long)intent.ExpectedEngagementRevision, rules.Version,
                authored.ExpectedRuleContentDigest, authored.ExpectedServiceHistoryDigest, frozen,
                Array.AsReadOnly(services.Where((_, index) => considered.Contains(records[index]))
                    .Select(item => item.ServiceRecordId).ToArray()), "partner_evaluation_required",
                "conditionally_compatible", authored.Rationale, staff.StaffMemberId, staff.UserId, staff.Revision,
                duty.DesignationId, duty.Revision, duty.SourceReference, intent.Header.OriginalActor, decisionAt);
            return Result<(PartnerIndependenceEvaluationView, IndependenceRuleVersionView)>.Success((evaluation,
                IndependenceRecordValidation.Freeze(rules)));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Failure($"The frozen evaluation cannot be verified: {exception.Message}");
        }
    }

    static Result<(PartnerIndependenceEvaluationView, IndependenceRuleVersionView)> Failure(string message) =>
        Result<(PartnerIndependenceEvaluationView, IndependenceRuleVersionView)>.Failure(
            new RequestError(RequestErrorKind.Conflict, message, false));

    sealed record Step(NativeFactReference Fact, bool Applied);
}
