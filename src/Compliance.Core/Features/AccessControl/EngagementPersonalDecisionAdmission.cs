using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Admits a personal engagement decision (management acknowledgement or partner evaluation) as a root native
///     fact of the engagement owner. Admission compares the expected engagement content revision with the owner,
///     reads the exact service epoch, and freezes the complete service manifest, the authored content and the exact
///     source uses the kind requires into the intent before the 202 acknowledgment is frozen on the root fact. An
///     exact retry replays the original acknowledgment even after the engagement, the service history or any source
///     changed; nothing about the decision is approved by admission alone.
/// </summary>
sealed class EngagementPersonalDecisionAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, ProfessionalDutyAuthorityReader duties,
    CurrentRatifiedIndependenceRulesReader ratifiedRules, EngagementSourcePins pins, TimeProvider clock, IDomainEventReader events)
{
    const string InitialEpisode = "initial";

    public ValueTask<Result<OperationAcceptedResponse>> AcknowledgeAsync(
        IRequestContext<AcknowledgeEngagementManagement> context, CancellationToken ct)
    {
        var request = context.Request;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Fail(RequestErrorKind.Forbidden,
                "Management responsibility requires the authenticated client's personal HTTP acknowledgement.");
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            request.AcknowledgementId == Uuid.Empty || request.CompleteServiceRecordIds is null ||
            !ServiceEngagementRules.Bounded(request.Statement) ||
            request.ExpectedEngagementRevision < 0 || request.ExpectedServiceEpoch < 0)
            return Fail(RequestErrorKind.Validation,
                "Management responsibility must be acknowledged personally by the attributed owning-client member.");
        var actor = AccessGrantActor.From(context, request.TenantId);
        return AdmitAsync(context, new Spec(request.TenantId, request.EngagementId, request.AcknowledgementId,
            ServiceEngagementIndependence.ManagementKind, EngagementPersonalDecisionKind.ManagementAcknowledgement,
            request.ExpectedEngagementRevision, request.ExpectedServiceEpoch, actor,
            AuthoredIntentDigest.Compute(request),
            (frozen, token) => BuildManagementAsync(frozen, request.CompleteServiceRecordIds, request.Statement, userId,
                token)), ct);
    }

    public ValueTask<Result<OperationAcceptedResponse>> EvaluateAsync(
        IRequestContext<RecordPartnerIndependenceEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("ProfessionalEngagementPartnerAuthorizer must reject this actor.");
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty || request.EvaluationId == Uuid.Empty ||
            request.ExpectedEngagementRevision < 0 || request.ExpectedServiceEpoch < 0 || request.RuleVersion <= 0 ||
            !ValidDigest(request.ExpectedRuleContentDigest) || !ValidDigest(request.ExpectedServiceHistoryDigest) ||
            !ServiceEngagementRules.Bounded(request.Rationale))
            return Fail(RequestErrorKind.Validation,
                "A partner evaluation requires current tenant-scoped professional authority, ratified rules and bounded evidence.");
        var actor = ActorReference.ForFirmStaff(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return AdmitAsync(context, new Spec(request.TenantId, request.EngagementId, request.EvaluationId,
            ServiceEngagementIndependence.PartnerKind, EngagementPersonalDecisionKind.PartnerEvaluation,
            request.ExpectedEngagementRevision, request.ExpectedServiceEpoch, actor,
            AuthoredIntentDigest.Compute(request),
            (frozen, token) => BuildPartnerAsync(frozen, request, userId, token)), ct);
    }

    async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync(IExecutionContext context, Spec spec,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var realm = IndependenceContent.Realm(spec.TenantId);
        var stream = new ServiceEngagementIndependence(spec.TenantId, spec.EngagementId).Stream;
        var provenance = new OperationProvenance(spec.DecisionId, spec.DecisionId, null, spec.CommandKind, spec.Actor,
            stream, InitialEpisode, (ulong)spec.ExpectedRevision, spec.AuthoredDigest, now, stream, stream, FirstOwner: stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        ulong intentRevision = 0;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ServiceEngagementIndependence>(binding,
            () => new ServiceEngagementIndependence(spec.TenantId, spec.EngagementId, spec.DecisionId),
            owner => owner.Acceptance, async (owner, token) =>
            {
                var frozen = await FreezeAsync(spec, owner, provenance, now, realm, token).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                intent = frozen.Value.Intent;
                intentRevision = owner.Revision;
                return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
            },
            (ServiceEngagementIndependence owner, ImmutableContentRef acknowledgment) =>
                owner.AdmitPersonalDecision(provenance, intent!, intentRevision, acknowledgment)), context, ct).ConfigureAwait(false);
        return await IndependenceContent.ReadAcknowledgmentAsync(content, realm, outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(Spec spec,
        ServiceEngagementIndependence engagement, OperationProvenance provenance, DateTimeOffset now, string realm,
        CancellationToken ct)
    {
        var prepared = engagement.PreparePersonalDecision(spec.DecisionId, spec.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        var clientService = await reader.HydrateAsync(new ClientServiceIndependence(spec.TenantId), ct)
            .ConfigureAwait(false);
        if ((long)clientService.Epoch != spec.ExpectedServiceEpoch)
            return IndependenceContent.Failure<(ImmutableContentRef, FirstResponseDescriptor)>(
                RequestErrorKind.Conflict,
                "Reload the service epoch of this client before deciding on its engagement.");

        ServiceEngagementView view;
        IReadOnlyList<NonattestServiceView> services;
        try
        {
            view = await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm,
                engagement.LatestResult!, ct).ConfigureAwait(false);
            services = await ClientServiceHistory.ReadAsync(events, content, clientService, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return IndependenceContent.Failure<(ImmutableContentRef, FirstResponseDescriptor)>(
                RequestErrorKind.Conflict, $"The engagement or service history cannot be verified: {exception.Message}",
                exception is ImmutableContentUnavailableException);
        }

        var completeServices = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            EngagementPersonalDecisionIntent.ServiceItemSchema, services, SelectionOrder.Authored, ct)
            .ConfigureAwait(false);
        var history = new Frozen(spec, engagement, view, clientService, services, completeServices, now, realm);
        var built = await spec.Build(history, ct).ConfigureAwait(false);
        if (!built.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(built.Error!);
        var (authored, extra) = built.Value;

        var serviceUse = new SourceUseRequirement(clientService.Stream,
            clientService.LatestService is { } latest
                ? NativeFactReference.Of(clientService.Stream, latest)
                : new NativeFactReference(clientService.Stream, Uuid.Empty, 0),
            clientService.Epoch, SourceUsePurpose.EngagementPersonalDecision, completeServices.Content);
        var requirements = extra.Append(serviceUse)
            .OrderBy(requirement => EngagementPersonalDecisionEffects.StreamKey(requirement.SourceOwner),
                StreamKeyOrder.Instance).ToArray();
        var required = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            EngagementPersonalDecisionIntent.UseItemSchema, requirements, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);

        var header = new DurableIntentHeader(spec.DecisionId, provenance.Owner, provenance.Owner, provenance.Owner,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        var intent = await IndependenceContent.WriteAsync(content, realm, EngagementPersonalDecisionIntent.Schema,
            new EngagementPersonalDecisionIntent(header, spec.DecisionId, spec.Kind, spec.EngagementId,
                (ulong)spec.ExpectedRevision, clientService.Epoch, completeServices, authored, required), ct)
            .ConfigureAwait(false);
        var acknowledgment = await IndependenceContent.WriteAsync(content, realm, "operation-accepted-response.v1",
            new OperationAcceptedResponse(spec.DecisionId, EvaluateClientIndependenceHandler.StatusUrlTemplate
                .Replace("{tenant_id}", spec.TenantId.ToString(), StringComparison.Ordinal)
                .Replace("{operation_id}", spec.DecisionId.ToString(), StringComparison.Ordinal), now), ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent,
            new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
                "operation-accepted-response.v1", new StandardResponseBody(acknowledgment))));
    }

    async ValueTask<Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>> BuildManagementAsync(
        Frozen frozen, IReadOnlyList<Uuid> serviceRecordIds, string statement, Uuid userId, CancellationToken ct)
    {
        var known = frozen.Services.Select(service => service.ServiceRecordId).ToArray();
        if (serviceRecordIds.Distinct().Count() != serviceRecordIds.Count || serviceRecordIds.Count != known.Length ||
            !serviceRecordIds.ToHashSet().SetEquals(known))
            return Conflict(
                "Reload the complete client facts and engagement revision before acknowledging management responsibility.");
        var ids = await content.WriteSelectionAsync(frozen.Realm, Uuid.CreateVersion4(),
            EngagementPersonalDecisionIntent.IdItemSchema, serviceRecordIds.ToArray(), SelectionOrder.Authored, ct)
            .ConfigureAwait(false);
        return Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>.Success((
            new ManagementAcknowledgementContent(ids, statement, userId), []));
    }

    async ValueTask<Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>> BuildPartnerAsync(
        Frozen frozen, RecordPartnerIndependenceEvaluation request, Uuid userId, CancellationToken ct)
    {
        try
        { return await BuildPartnerCoreAsync(frozen, request, userId, ct).ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>.Failure(IndependenceCatalogContent.Failure(error));
        }
    }

    async ValueTask<Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>> BuildPartnerCoreAsync(
        Frozen frozen, RecordPartnerIndependenceEvaluation request, Uuid userId, CancellationToken ct)
    {
        var tenantId = frozen.Spec.TenantId;
        var duty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.EngagementPartner, tenantId, ct)
            .ConfigureAwait(false);
        if (duty is null)
            return Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>.Failure(
                new RequestError(RequestErrorKind.Forbidden,
                    "The actor no longer has a current partner designation for this client."));
        var rules = await ratifiedRules.ReadActiveAsync(ct).ConfigureAwait(false);
        if (rules is null || rules.Version != request.RuleVersion ||
            IndependenceSourceDigest.RuleContent(rules.Content) != request.ExpectedRuleContentDigest)
            return Conflict("The active ratified rules changed or are unavailable; reload the exact version and digest.",
                true);
        if (IndependenceSourceDigest.Services(frozen.Services) != request.ExpectedServiceHistoryDigest)
            return Conflict("The draft or complete client service history changed; reload the current portfolio.", true);

        var now = frozen.Now;
        if (duty.Designation.DesignatedAt > now || duty.Staff.RecordedAt > now || rules.RecordedAt > now ||
            rules.Ratification?.RecordedAt > now)
            return Conflict("Partner evaluation sources are dated after the evaluation.");
        var records = frozen.Services.Select(service => new NonattestServiceRecord(tenantId,
            service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
            service.Content.EndedOn, service.Content.FirmStaffMemberIds,
            service.Content.InvolvedManagementFunctions)).ToArray();
        var policy = new IndependenceRuleSet(rules.Version, rules.Content.LookBackMonths,
            rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                IndependenceWire.Classification(rule.Classification),
                IndependenceWire.Classification(rule.ManagementFunctionsClassification))).ToArray());
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(tenantId, policy, records,
            frozen.Engagement.Content.PeriodStart, partnerEvaluationRecorded: false);
        if (decision.Code != IndependenceDecisionCode.PartnerEvaluationRequired ||
            decision.EvaluationOutcome != IndependenceEvaluationOutcome.ConditionallyCompatible)
            return Conflict(
                "Partner evaluation is available only for a fully classified conditionally compatible service history; impairment and unclassified services remain blocked.");

        // Pin the exact sources the evaluation relies on; each is held by its own use receiver before authorization.
        if (await pins.PartnerAsync(tenantId, userId, duty, ct).ConfigureAwait(false) is not { } partnerPins ||
            await pins.RulesAsync(rules, ct).ConfigureAwait(false) is not { } rulePins)
            return Conflict("A partner source changed while the evaluation was prepared; reload and retry.", true);
        SourceUseRequirement[] uses = [.. partnerPins, .. rulePins];
        return Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>.Success((
            new PartnerEvaluationContent(request.RuleVersion, request.ExpectedRuleContentDigest,
                request.ExpectedServiceHistoryDigest, request.Rationale), uses));
    }

    static bool ValidDigest(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)> Conflict(string message,
        bool transient = false) =>
        Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>.Failure(
            new RequestError(RequestErrorKind.Conflict, message, transient));

    static ValueTask<Result<OperationAcceptedResponse>> Fail(RequestErrorKind kind, string message) =>
        ValueTask.FromResult(Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message)));

    sealed record Spec(Uuid TenantId, Uuid EngagementId, Uuid DecisionId, string CommandKind,
        EngagementPersonalDecisionKind Kind, long ExpectedRevision, long ExpectedServiceEpoch, ActorReference Actor,
        string AuthoredDigest,
        Func<Frozen, CancellationToken,
            ValueTask<Result<(EngagementPersonalDecisionContent, IReadOnlyList<SourceUseRequirement>)>>> Build);

    sealed record Frozen(Spec Spec, ServiceEngagementIndependence Owner, ServiceEngagementView Engagement,
        ClientServiceIndependence ClientService, IReadOnlyList<NonattestServiceView> Services,
        ImmutableSelectionManifest CompleteServices, DateTimeOffset Now, string Realm);
}
