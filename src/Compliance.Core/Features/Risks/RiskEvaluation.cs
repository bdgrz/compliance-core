using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A risk's assessments, chosen treatment, and personal acceptances under M0-D10.
///     It shares the risk's identity but keeps its own revision sequence.
/// </summary>
public sealed class RiskEvaluation : Aggregate
{
    public const string Inherent = "inherent";
    public const string Target = "target";
    public const string Residual = "residual";
    public const string ComplianceLead = "compliance_lead";
    public const string Executive = "executive";
    const int MaximumRationaleLength = 4000;
    static readonly string[] Phases = [Inherent, Target, Residual];
    static readonly string[] TreatmentKinds = ["mitigate", "accept", "transfer", "avoid"];

    readonly Uuid _tenantId;
    readonly List<RiskAssessmentRecorded> _assessments = [];
    readonly List<RiskAcceptanceView> _acceptances = [];
    readonly Dictionary<Uuid, RiskAccepted> _acceptanceFacts = [];
    readonly Uuid _selectedLogicalId;
    DomainEvent? _selectedFact;
    ActorReference? _lastChangedBy;
    DateTimeOffset? _lastChangedAt;

    public long Revision { get; private set; }
    public Uuid ProgramId { get; private set; }
    public RiskTreatmentView? Treatment { get; private set; }

    public RiskEvaluation(Uuid tenantId, Uuid riskId, Uuid selectedLogicalId = default)
        : base(riskId, new EventStreamAddress(tenantId.ToString(), "risk-evaluations",
            riskId.ToString()))
    {
        _tenantId = tenantId;
        _selectedLogicalId = selectedLogicalId;
        On<RiskAssessmentRecorded>(ev =>
        {
            Apply(ev.ProgramId, ev.Revision, ev.Assessment.Assessor, ev.Assessment.AssessedAt);
            Observe(ev.Assessment.AssessmentId, ev);
            _assessments.Add(ev);
        });
        On<RiskTreatmentChosen>(ev =>
        {
            Apply(ev.ProgramId, ev.Revision, ev.Treatment.ChosenBy, ev.Treatment.ChosenAt);
            Observe(ev.CommandId, ev);
            Treatment = ev.Treatment;
        });
        On<RiskAccepted>(ev =>
        {
            Apply(ev.ProgramId, ev.Revision, ev.Acceptance.Approver, ev.Acceptance.AcceptedAt);
            Observe(ev.Acceptance.AcceptanceId, ev);
            _acceptances.Add(ev.Acceptance);
            _acceptanceFacts[ev.Acceptance.AcceptanceId] = ev;
        });
    }

    /// <summary>The committed fact that recorded one acceptance, so a dependent can prove the exact source.</summary>
    public RiskAccepted? AcceptanceFact(Uuid acceptanceId) => _acceptanceFacts.GetValueOrDefault(acceptanceId);

    public RiskAssessmentView? Latest(string phase) =>
        _assessments.LastOrDefault(fact => fact.Assessment.Phase == phase)?.Assessment;

    public RiskAssessmentView? FindAssessment(Uuid assessmentId) =>
        _assessments.Find(fact => fact.Assessment.AssessmentId == assessmentId)?.Assessment;

    public RiskAcceptanceView? FindAcceptance(Uuid acceptanceId) =>
        _acceptances.Find(acceptance => acceptance.AcceptanceId == acceptanceId);

    /// <summary>Returns the immutable original assessment before any mutable risk/method/treatment lookup.</summary>
    public Result<RiskAssessmentView>? ReplayAssessment(RecordRiskAssessment request, Uuid assessorMemberId)
    {
        if (request.CommandId == _selectedLogicalId && _selectedFact is not null && _selectedFact is not RiskAssessmentRecorded)
            return Result<RiskAssessmentView>.Failure(IdentityConflict());
        var original = _assessments.Find(fact => fact.Assessment.AssessmentId == request.CommandId);
        if (original is null)
            return null;
        return request.TenantId == _tenantId && request.RiskId == Id && request.ProgramId == original.ProgramId &&
               original.OriginalExpectedRevision == request.ExpectedRevision &&
               original.AuthoredIntentSha256 == AuthoredIntentDigest.Compute(request) &&
               original.Assessment.Assessor.Kind == "member" && original.Assessment.Assessor.Id == assessorMemberId.ToString()
            ? Result<RiskAssessmentView>.Success(original.Assessment)
            : Result<RiskAssessmentView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The assessment identity already accepted another actor or authored request."));
    }

    public CommandFailure? RecordAssessment(Uuid programId, long expectedRevision,
        Uuid assessmentId, RiskMethodVersionView method, string phase, int likelihood,
        int impact, string rationale, Uuid assessorMemberId, string assessorDisplay,
        DateTimeOffset assessedAt, bool hasAcceptedControlTreatment = false)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (assessmentId == Uuid.Empty)
            return CommandFailure.InvalidContent("An assessment requires its authored command identity.");
        var authored = new RecordRiskAssessment(assessmentId, _tenantId, programId, Id, expectedRevision,
            method.Version, phase, likelihood, impact, rationale);
        if (ReplayAssessment(authored, assessorMemberId) is { } retry)
            return retry.IsSuccess ? null : CommandFailure.StateConflict(retry.Error!.Message!);
        var scope = CheckScope(programId, expectedRevision);
        if (scope is not null)
            return scope;
        if (method.TenantId != _tenantId || method.ProgramId != programId)
            return CommandFailure.InvalidContent("The risk method version belongs to another program.");
        if (!Phases.Contains(phase))
            return CommandFailure.InvalidContent("The assessment phase must be inherent, target, or residual.");
        if (likelihood is < 1 or > 5 || impact is < 1 or > 5)
            return CommandFailure.InvalidContent("Likelihood and impact must each be from 1 to 5.");
        if (InvalidRationale(rationale))
            return CommandFailure.InvalidContent("An assessment requires a rationale of at most 4000 characters.");
        if (phase != Inherent)
        {
            if (Latest(Inherent) is not { } inherent)
                return CommandFailure.StateConflict("Record an inherent assessment first.");
            if (inherent.MethodVersionId != method.MethodVersionId)
                return CommandFailure.StateConflict(
                    "The method changed since the inherent assessment. Reassess the inherent risk first.");
        }
        if (phase == Residual && Treatment is null)
            return CommandFailure.StateConflict(
                "A residual assessment requires a chosen treatment.");
        if (phase == Residual && Treatment is { Kind: "mitigate" } && !hasAcceptedControlTreatment)
            return CommandFailure.StateConflict(
                "A residual assessment under mitigate requires an independently accepted control treatment.");
        RaiseEvent(new RiskAssessmentRecorded(_tenantId, programId, Id, Revision + 1,
            new RiskAssessmentView(assessmentId, phase, method.MethodVersionId, method.Version,
                likelihood, impact, likelihood * impact, rationale.Trim(),
                ActorReference.ForMember(assessorMemberId, assessorDisplay), assessedAt))
        {
            OriginalExpectedRevision = expectedRevision,
            AuthoredIntentSha256 = AuthoredIntentDigest.Compute(authored),
        });
        return null;
    }

    public Result? ReplayTreatment(ChooseRiskTreatment request, Uuid memberId)
    {
        if (_selectedFact is null)
            return null;
        return _selectedFact is RiskTreatmentChosen original && request.TenantId == _tenantId &&
               request.RiskId == Id && request.ProgramId == original.ProgramId &&
               original.OriginalExpectedRevision == request.ExpectedRevision &&
               original.AuthoredIntentSha256 == AuthoredIntentDigest.Compute(request) &&
               original.Treatment.ChosenBy.Kind == "member" && original.Treatment.ChosenBy.Id == memberId.ToString()
            ? Result.Success : Result.Failure(IdentityConflict());
    }

    public CommandFailure? ChooseTreatment(ChooseRiskTreatment request, Uuid memberId, string display, DateTimeOffset at)
    {
        if (request.CommandId == Uuid.Empty || request.CommandId != _selectedLogicalId ||
            request.TenantId != _tenantId || request.RiskId != Id)
            return CommandFailure.InvalidContent("Treatment requires its selected command identity and exact owner.");
        if (ReplayTreatment(request, memberId) is { } original)
            return original.IsSuccess ? null : CommandFailure.StateConflict(original.Error!.Message!);
        return ChooseTreatment(request.ProgramId, request.ExpectedRevision, request.Kind, request.Rationale,
            memberId, display, at, request.CommandId, AuthoredIntentDigest.Compute(request));
    }

    public CommandFailure? ChooseTreatment(Uuid programId, long expectedRevision, string kind,
        string rationale, Uuid actorMemberId, string actorDisplay, DateTimeOffset chosenAt,
        Uuid? commandId = null, string? authoredIntentSha256 = null)
    {
        var scope = CheckScope(programId, expectedRevision);
        if (scope is not null)
            return scope;
        if (!TreatmentKinds.Contains(kind))
            return CommandFailure.InvalidContent("The treatment must be mitigate, accept, transfer, or avoid.");
        if (InvalidRationale(rationale))
            return CommandFailure.InvalidContent("A treatment requires a rationale of at most 4000 characters.");
        if (Latest(Inherent) is null)
            return CommandFailure.StateConflict("Record an inherent assessment before choosing a treatment.");
        RaiseEvent(new RiskTreatmentChosen(_tenantId, programId, Id, Revision + 1,
            new RiskTreatmentView(kind, rationale.Trim(),
                ActorReference.ForMember(actorMemberId, actorDisplay), chosenAt))
        {
            CommandId = commandId,
            OriginalExpectedRevision = commandId is null ? null : expectedRevision,
            AuthoredIntentSha256 = authoredIntentSha256,
        });
        return null;
    }

    public Result<RiskAcceptanceView>? ReplayAcceptance(AcceptRisk request, Uuid memberId)
    {
        if (request.CommandId == _selectedLogicalId && _selectedFact is not null && _selectedFact is not RiskAccepted)
            return Result<RiskAcceptanceView>.Failure(IdentityConflict());
        if (!_acceptanceFacts.TryGetValue(request.CommandId, out var original))
            return null;
        return request.TenantId == _tenantId && request.RiskId == Id && request.ProgramId == original.ProgramId &&
               original.OriginalExpectedRevision == request.ExpectedRevision &&
               original.AuthoredIntentSha256 == AuthoredIntentDigest.Compute(request) &&
               original.Acceptance.ApproverMemberId == memberId
            ? Result<RiskAcceptanceView>.Success(original.Acceptance)
            : Result<RiskAcceptanceView>.Failure(IdentityConflict());
    }

    public CommandFailure? Accept(Uuid programId, long expectedRevision, Uuid acceptanceId,
        Uuid residualAssessmentId, RiskMethodVersionView method, string authority,
        bool approverHoldsAuthority, DateTimeOffset expiresAt, string rationale,
        Uuid approverMemberId, string approverDisplay, DateTimeOffset acceptedAt,
        Uuid? ownerMemberId = null, SeparationOfDutiesWaiverProof? waiver = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (acceptanceId == Uuid.Empty)
            return CommandFailure.InvalidContent("Acceptance requires its authored command identity.");
        var authored = new AcceptRisk(acceptanceId, _tenantId, programId, Id, expectedRevision,
            residualAssessmentId, authority, expiresAt, rationale, waiver?.WaiverId);
        if (ReplayAcceptance(authored, approverMemberId) is { } original)
            return original.IsSuccess ? null : CommandFailure.StateConflict(original.Error!.Message!);
        var scope = CheckScope(programId, expectedRevision);
        if (scope is not null)
            return scope;
        if (authority is not (ComplianceLead or Executive))
            return CommandFailure.InvalidContent("The approver authority must be compliance_lead or executive.");
        if (InvalidRationale(rationale))
            return CommandFailure.InvalidContent("An acceptance requires a rationale of at most 4000 characters.");
        if (expiresAt <= acceptedAt || expiresAt > acceptedAt.AddMonths(12))
            return CommandFailure.InvalidContent("An acceptance must expire within 12 months.");
        if (Treatment is not { Kind: "accept" })
            return CommandFailure.StateConflict("Only a risk whose chosen treatment is accept can be accepted.");
        if (Latest(Residual) is not { } residual || residual.AssessmentId != residualAssessmentId)
            return CommandFailure.StateConflict("Accept the current residual assessment.");
        if (method.MethodVersionId != residual.MethodVersionId)
            return CommandFailure.StateConflict("The residual assessment uses another method version.");
        if (!approverHoldsAuthority)
            return CommandFailure.ActorProhibited($"The approver does not hold {authority} acceptance authority.");
        if (authority == ComplianceLead &&
            (method.AppetiteThreshold is not { } appetite || residual.Score > appetite))
            return CommandFailure.ActorProhibited(
                "Accepting a risk above appetite, or while appetite is unset, requires an executive approver.");
        if (residual.Assessor.Kind == "member" &&
            residual.Assessor.Id == approverMemberId.ToString())
            return CommandFailure.ActorProhibited("The residual assessor cannot accept the same risk.");
        if (waiver is not null && (waiver.ToScope() != new SeparationOfDutiesWaiverProofScope(_tenantId,
                waiver.WaiverId, approverMemberId, approverMemberId, SeparationOfDutiesActions.Approve,
                SeparationOfDutiesRecordTypes.Risk, Id, residualAssessmentId, expectedRevision) ||
                acceptedAt < waiver.ValidFrom || acceptedAt >= waiver.ValidUntil ||
                waiver.ApprovalFact.Source != new EventStreamAddress(_tenantId.ToString(),
                    "separation-of-duties-waivers", waiver.WaiverId.ToString()) ||
                waiver.ApprovalFact.EventId == Uuid.Empty || waiver.ApprovalFact.ResourcePosition != 1 ||
                waiver.ApprovalFact.AggregateVersion != 2))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and risk revision.");
        if (ownerMemberId == approverMemberId && waiver is null)
            return CommandFailure.ActorProhibited(
                "The risk owner cannot accept their own risk without a separation-of-duties waiver.");
        RaiseEvent(new RiskAccepted(_tenantId, programId, Id, Revision + 1,
            new RiskAcceptanceView(acceptanceId, residualAssessmentId, residual.Score,
                method.AppetiteThreshold, approverMemberId,
                ActorReference.ForMember(approverMemberId, approverDisplay), authority,
                rationale.Trim(), acceptedAt, expiresAt)
            {
                SeparationOfDutiesWaiverId = waiver?.WaiverId,
            })
        {
            OriginalExpectedRevision = expectedRevision,
            AuthoredIntentSha256 = AuthoredIntentDigest.Compute(authored),
            WaiverProof = waiver,
        });
        return null;
    }

    void Observe(Uuid? logicalId, DomainEvent fact)
    {
        if (_selectedLogicalId == Uuid.Empty || logicalId != _selectedLogicalId)
            return;
        if (_selectedFact is not null)
            throw new InvalidOperationException("One logical evaluation operation is accepted once on its owner.");
        _selectedFact = fact;
    }

    static RequestError IdentityConflict() => new(RequestErrorKind.Conflict,
        "This evaluation command identity already accepted another actor, command or authored intent.");

    public RiskEvaluationView ToView() => new(_tenantId, ProgramId, Id, Revision, "unassessed",
        [.. _assessments.Select(static fact => fact.Assessment)], Treatment, [.. _acceptances], null, _lastChangedBy, _lastChangedAt);

    CommandFailure? CheckScope(Uuid programId, long expectedRevision)
    {
        if (Revision > 0 && ProgramId != programId)
            return CommandFailure.MissingRecord("The risk was not found.");
        return expectedRevision != Revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk evaluation",
                Revision))
            : null;
    }

    void Apply(Uuid programId, long revision, ActorReference actor, DateTimeOffset at)
    {
        ProgramId = programId;
        Revision = revision;
        _lastChangedBy = actor;
        _lastChangedAt = at;
    }

    static bool InvalidRationale(string? rationale) =>
        string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > MaximumRationaleLength;
}
