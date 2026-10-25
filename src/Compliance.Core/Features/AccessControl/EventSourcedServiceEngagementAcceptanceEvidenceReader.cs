using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Builds acceptance evidence only from current client, firm-duty, ratification and boundary sources. The engagement
///     draft is read from its own owner, the complete service history from the client service owner and the paired
///     personal decisions from their own owners.
/// </summary>
sealed class EventSourcedServiceEngagementAcceptanceEvidenceReader(IAggregateReader reader, BoundaryReader boundarySources,
    FirmStaffViews staffViews, ProfessionalDutyAuthorityReader duties, CurrentRatifiedIndependenceRulesReader rulesReader,
    IBoundaryDirectoryReader boundaries, BoundaryHistoryReadConsistency boundaryConsistency,
    EngagementPersonalDecisionViews personalDecisions, ServiceEngagementViews engagements,
    IImmutableContentStore content, TimeProvider clock) : IServiceEngagementAcceptanceEvidenceReader
{
    public async ValueTask<Result<ServiceEngagementAcceptanceEvidence>> ReadCurrentAsync(Uuid tenantId,
        Uuid engagementId, Uuid actorUserId, CancellationToken ct)
    {
        try
        { return await ReadCurrentCoreAsync(tenantId, engagementId, actorUserId, ct).ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ServiceEngagementAcceptanceEvidence>.Failure(IndependenceCatalogContent.Failure(error));
        }
    }

    async ValueTask<Result<ServiceEngagementAcceptanceEvidence>> ReadCurrentCoreAsync(Uuid tenantId,
        Uuid engagementId, Uuid actorUserId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty || actorUserId == Uuid.Empty)
            return Refuse(RequestErrorKind.Validation, "An exact client engagement and personal partner identity are required.");

        var duty = await duties.ReadCurrentAsync(actorUserId, FirmProfessionalDuty.EngagementPartner,
            tenantId, ct).ConfigureAwait(false);
        if (duty is null)
            return Refuse(RequestErrorKind.Forbidden, "The actor has no current partner designation for this client.");
        var rules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        if (rules is null)
            return Refuse(RequestErrorKind.Conflict, "Current ratified independence rules are unavailable.", true);

        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
            .ConfigureAwait(false);
        var client = await reader.HydrateAsync(new ClientServiceIndependence(tenantId), ct).ConfigureAwait(false);
        if (owner.Status != "draft" || owner.LatestResult is not { } latestDraft)
            return Refuse(RequestErrorKind.Conflict, "The exact service engagement draft is unavailable.", true);
        ServiceEngagementView engagement;
        IReadOnlyList<ServiceEngagementView> history;
        IReadOnlyList<NonattestServiceView> serviceHistory;
        try
        {
            engagement = await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content,
                IndependenceContent.Realm(tenantId), latestDraft, ct).ConfigureAwait(false);
            history = await engagements.HistoryAsync(tenantId, engagementId, ct).ConfigureAwait(false) ?? [];
            serviceHistory = await ClientServiceHistory.ReadAsync(content, client, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Refuse(RequestErrorKind.Conflict, "The engagement or service history cannot be verified.",
                exception is ImmutableContentUnavailableException);
        }

        if (history.Any(item => item.RecordedAt > clock.GetUtcNow()) ||
            serviceHistory.Any(item => item.RecordedAt > clock.GetUtcNow()))
            return Refuse(RequestErrorKind.Conflict, "A current source has an invalid future timestamp.", true);

        var partner = duty.Staff;
        if (await CurrentAsync(partner.StaffMemberId, partner.UserId, ct).ConfigureAwait(false) is not { } currentPartner ||
            !MatchesCurrentStaff(currentPartner, partner.StaffMemberId, partner.UserId, partner.Revision,
                engagement.Content.Practice))
            return Refuse(RequestErrorKind.Conflict,
                "The designated partner no longer matches one current active firm-staff directory row.", true);

        var proposed = engagement.Staff.Where(item => item.IsCurrent).ToArray();
        if (proposed.Length == 0)
            return Refuse(RequestErrorKind.Conflict, "The engagement has no current proposed professional team.", true);
        var currentStaff = new List<FirmStaffMemberView>(proposed.Length);
        foreach (var proposal in proposed)
        {
            if (await CurrentAsync(proposal.StaffMemberId, proposal.UserId, ct).ConfigureAwait(false) is not { } match ||
                !MatchesCurrentStaff(match, proposal.StaffMemberId, proposal.UserId,
                    proposal.DirectoryStaffRevision, proposal.Practice))
                return Refuse(RequestErrorKind.Conflict,
                    "Every proposed assignee must match one current active firm-staff directory row.", true);
            currentStaff.Add(match);
        }

        var serviceIds = serviceHistory.Select(item => item.ServiceRecordId).ToArray();
        var acknowledgement = (await personalDecisions.ManagementAcknowledgementsAsync(tenantId, engagementId, ct)
            .ConfigureAwait(false))
            .Where(item => item.EngagementRevision == engagement.Revision && item.UserId != partner.UserId &&
                item.CompleteServiceRecordIds.Count == serviceIds.Length &&
                item.CompleteServiceRecordIds.ToHashSet().SetEquals(serviceIds))
            .OrderByDescending(item => item.RecordedAt).FirstOrDefault();
        if (acknowledgement is null)
            return Refuse(RequestErrorKind.Conflict,
                "A separate current client management acknowledgement for the complete service history is required.", true);

        PartnerIndependenceEvaluationView? evaluation = null;
        if (engagement.Content.Practice == "attest")
        {
            var policy = RuleSet(rules);
            var sourceRecords = serviceHistory.Select(item => new NonattestServiceRecord(tenantId,
                item.Content.ServiceEngagementId, item.Content.ServiceType, item.Content.StartedOn,
                item.Content.EndedOn, item.Content.FirmStaffMemberIds,
                item.Content.InvolvedManagementFunctions)).ToArray();
            var decision = IndependenceCompartments.CanAcceptAttestEngagement(tenantId, policy,
                sourceRecords, engagement.Content.PeriodStart, partnerEvaluationRecorded: false);
            if (decision.Code == IndependenceDecisionCode.PartnerEvaluationRequired)
            {
                evaluation = (await personalDecisions.PartnerEvaluationsAsync(tenantId, engagementId, ct)
                    .ConfigureAwait(false)).LastOrDefault(item =>
                    MatchesEvaluation(item, tenantId, engagement, rules, serviceHistory, partner,
                        duty.Designation, clock.GetUtcNow()));
                if (evaluation is null)
                    return Refuse(RequestErrorKind.Conflict,
                        "The current conditional service history requires a recorded designated-partner evaluation.", true);
            }
            else if (decision.Code != IndependenceDecisionCode.Allowed)
                return Refuse(RequestErrorKind.Conflict,
                    "Impairing, unclassified or unreadable service history cannot be overridden by partner evaluation.", true);
        }

        var boundaryResult = await ReadSelectedBoundaryAsync(tenantId, engagement, ct).ConfigureAwait(false);
        if (!boundaryResult.IsSuccess)
            return Result<ServiceEngagementAcceptanceEvidence>.Failure(boundaryResult.Error!);

        var now = clock.GetUtcNow();
        if (acknowledgement.RecordedAt > now || rules.RecordedAt > now || partner.RecordedAt > now ||
            currentStaff.Any(item => item.RecordedAt > now) || evaluation is { RecordedAt: var evaluatedAt } && evaluatedAt > now)
            return Refuse(RequestErrorKind.Conflict, "Acceptance evidence contains a future-dated source.", true);

        var proof = new VerifiedEngagementAcceptance(tenantId, engagementId, engagement.Revision,
            Uuid.CreateVersion5(tenantId, $"partner-review-v1:{engagementId}:{engagement.Revision}:" +
                $"{rules.Version}:{IndependenceSourceDigest.Services(serviceHistory)}:{partner.UserId}"),
            partner.StaffMemberId, partner.UserId, duty.Designation.SourceReference,
            evaluation?.EvaluationId.ToString(), acknowledgement.AcknowledgementId,
            boundaryResult.Value?.Version, boundaryResult.Value?.Approval,
            Array.AsReadOnly(currentStaff.ToArray()), partner, duty.Designation.Revision, now, evaluation);

        // Source streams are independent of each other. Re-read the authority, rules, engagement and service
        // epoch after assembly; admission then freezes them and holds the epoch before authorizing.
        var currentDuty = await duties.ReadCurrentAsync(actorUserId, FirmProfessionalDuty.EngagementPartner,
            tenantId, ct).ConfigureAwait(false);
        var currentRules = await rulesReader.ReadActiveAsync(ct).ConfigureAwait(false);
        var currentOwner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
            .ConfigureAwait(false);
        var currentClient = await reader.HydrateAsync(new ClientServiceIndependence(tenantId), ct).ConfigureAwait(false);
        var currentBoundary = await ReadSelectedBoundaryAsync(tenantId, engagement, ct).ConfigureAwait(false);
        if (currentDuty != duty || currentRules is null || !SameRules(currentRules, rules) ||
            currentClient.Epoch != client.Epoch || currentOwner.Revision != owner.Revision ||
            !currentBoundary.IsSuccess || !SameBoundary(boundaryResult.Value, currentBoundary.Value))
            return Refuse(RequestErrorKind.Conflict,
                "Partner authority, rules, client facts or selected boundary changed during evidence assembly; reload and retry.", true);

        return Result<ServiceEngagementAcceptanceEvidence>.Success(new ServiceEngagementAcceptanceEvidence(proof, rules));
    }

    async ValueTask<Result<ResolvedBoundary?>> ReadSelectedBoundaryAsync(Uuid tenantId,
        ServiceEngagementView engagement, CancellationToken ct)
    {
        try
        {
            if (engagement.Content.ExaminationBoundary is not { } selected)
                return Result<ResolvedBoundary?>.Success(null);
            var source = await boundarySources.ReadAsync(tenantId, selected.BoundaryId, ct).ConfigureAwait(false);
            if (!source.IsCreated || !source.IsVisible || !source.IsVersionApproved(selected.VersionId))
                return RefuseBoundary("The selected boundary version is not a currently retained approved version.");
            var freshness = await boundaryConsistency.EnsureAsync(tenantId, selected.BoundaryId,
                source.Revision, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<ResolvedBoundary?>.Failure(freshness.Error!);
            var directory = await boundaries.GetAsync(tenantId, selected.BoundaryId, ct).ConfigureAwait(false);
            var version = await boundaries.GetVersionAsync(tenantId, selected.BoundaryId, selected.VersionId, ct)
                .ConfigureAwait(false);
            if (directory is null || directory.TenantId != tenantId || directory.BoundaryId != selected.BoundaryId ||
                version is not { Status: "approved" } || version.TenantId != tenantId ||
                version.BoundaryId != selected.BoundaryId || version.VersionId != selected.VersionId ||
                version.Revision != selected.Revision)
                return RefuseBoundary("The selected boundary version is missing, stale, foreign or not approved.");
            var decisionPage = await boundaries.ListDecisionsAsync(tenantId, selected.BoundaryId, 200, null, ct)
                .ConfigureAwait(false);
            if (decisionPage is null || decisionPage.NextCursor is not null ||
                decisionPage.Items.Any(item => item.TenantId != tenantId || item.BoundaryId != selected.BoundaryId))
                return RefuseBoundary("The complete selected-boundary decision history is unavailable or over its read limit.");
            var approvals = decisionPage.Items.Where(item => item.Outcome == "approve" &&
                item.VersionId == selected.VersionId && item.Revision == selected.Revision).ToArray();
            if (approvals is not [{ } approval] || approval.DecisionId == Uuid.Empty ||
                approval.DecidedAt == default || approval.DecidedAt > clock.GetUtcNow())
                return RefuseBoundary("The selected boundary does not have one current exact approval decision.");
            return Result<ResolvedBoundary?>.Success(new ResolvedBoundary(version, approval));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<ResolvedBoundary?>.Failure(BoundaryReadFailure.Of(failure));
        }
    }

    /// <summary>The one staff view both the staff record and the canonical-user claim agree on, or null.</summary>
    async ValueTask<FirmStaffMemberView?> CurrentAsync(Uuid staffMemberId, Uuid userId, CancellationToken ct)
    {
        var byStaff = await staffViews.GetAsync(staffMemberId, ct).ConfigureAwait(false);
        return byStaff is not null && byStaff.UserId == userId &&
            await staffViews.FindByUserAsync(userId, ct).ConfigureAwait(false) == byStaff ? byStaff : null;
    }

    static bool MatchesCurrentStaff(FirmStaffMemberView staff, Uuid staffMemberId, Uuid userId,
        long revision, string practice) =>
        staff.IsActive && staff.StaffMemberId == staffMemberId && staff.UserId == userId &&
        staff.Revision == revision && staff.Practice == practice;

    static bool MatchesEvaluation(PartnerIndependenceEvaluationView evaluation, Uuid tenantId,
        ServiceEngagementView engagement, IndependenceRuleVersionView rules,
        IReadOnlyList<NonattestServiceView> services, FirmStaffMemberView partner,
        FirmProfessionalDutyDesignationView duty, DateTimeOffset now) =>
        evaluation.TenantId == tenantId && evaluation.EngagementId == engagement.EngagementId &&
        evaluation.DraftRevision == engagement.Revision && evaluation.RuleVersion == rules.Version &&
        evaluation.RuleContentDigest == IndependenceSourceDigest.RuleContent(rules.Content) &&
        evaluation.ServiceHistoryDigest == IndependenceSourceDigest.Services(services) &&
        evaluation.CompleteServiceHistory.Select(item => item.ServiceRecordId)
            .SequenceEqual(services.Select(item => item.ServiceRecordId)) &&
        evaluation.PartnerStaffMemberId == partner.StaffMemberId && evaluation.PartnerUserId == partner.UserId &&
        evaluation.PartnerDirectoryStaffRevision == partner.Revision &&
        evaluation.DutyDesignationId == duty.DesignationId && evaluation.PartnerDutyRevision == duty.Revision &&
        evaluation.DutySourceReference == duty.SourceReference && evaluation.RecordedAt <= now &&
        evaluation.DecisionCode == "partner_evaluation_required" &&
        evaluation.Outcome == "conditionally_compatible" && evaluation.Actor.Kind == "firm_staff" &&
        evaluation.Actor.Id == partner.UserId.ToString();

    static bool SameRules(IndependenceRuleVersionView current, IndependenceRuleVersionView captured) =>
        current.Version == captured.Version && current.IsRatified && captured.IsRatified &&
        IndependenceSourceDigest.RuleContent(current.Content) == IndependenceSourceDigest.RuleContent(captured.Content) &&
        current.Ratification?.RatificationId == captured.Ratification?.RatificationId &&
        current.Ratification?.RuleContentDigest == captured.Ratification?.RuleContentDigest;

    static bool SameBoundary(ResolvedBoundary? first, ResolvedBoundary? second) =>
        first is null && second is null || first is not null && second is not null &&
        first.Version.TenantId == second.Version.TenantId && first.Version.BoundaryId == second.Version.BoundaryId &&
        first.Version.VersionId == second.Version.VersionId && first.Version.Revision == second.Version.Revision &&
        first.Approval.DecisionId == second.Approval.DecisionId && first.Approval.Revision == second.Approval.Revision;

    static IndependenceRuleSet RuleSet(IndependenceRuleVersionView rules) => new(rules.Version,
        rules.Content.LookBackMonths, rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(
            rule.ServiceType, Classification(rule.Classification), Classification(rule.ManagementFunctionsClassification))).ToArray());

    static IndependenceServiceClassification Classification(string value) => value switch
    {
        "compatible" => IndependenceServiceClassification.Compatible,
        "conditionally_compatible" => IndependenceServiceClassification.ConditionallyCompatible,
        "impairing" => IndependenceServiceClassification.Impairing,
        _ => throw new InvalidOperationException("An active ratified rule contained an invalid classification.")
    };

    static Result<ResolvedBoundary?> RefuseBoundary(string message) =>
        Result<ResolvedBoundary?>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    static Result<ServiceEngagementAcceptanceEvidence> Refuse(RequestErrorKind kind, string message,
        bool transient = false) => Result<ServiceEngagementAcceptanceEvidence>.Failure(
        new RequestError(kind, message, transient));

    sealed record ResolvedBoundary(BoundaryVersionView Version, BoundaryDecisionView Approval);
}
