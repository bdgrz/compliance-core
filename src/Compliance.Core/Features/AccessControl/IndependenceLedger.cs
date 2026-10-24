using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The remaining legacy client ledger. Service history, previews and engagement drafts moved to
///     <see cref="ClientServiceIndependence" />, <see cref="ClientIndependenceDecision" /> and
///     <see cref="ServiceEngagementIndependence" />; no registered command appends to this ledger any more. It keeps
///     only what the acceptance, actual-assignment, management-acknowledgement, partner-evaluation and directory
///     reevaluation facts still need until their owners replace them, and it is the source of the permanent
///     actual-assignment wall that the independence guards read.
/// </summary>
public sealed partial class IndependenceLedger : Aggregate
{
    readonly Uuid _tenantId;
    readonly List<NonattestServiceView> _services = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, object Response)> _decisions = [];
    readonly Dictionary<Uuid, ServiceEngagementView> _engagements = [];
    readonly Dictionary<Uuid, List<ServiceEngagementView>> _engagementHistory = [];
    readonly List<EngagementAssignment> _assignmentHistory = [];

    public IndependenceLedger(Uuid tenantId) : base(tenantId,
        new EventStreamAddress(tenantId.ToString(), "client-independence", tenantId.ToString()))
    {
        _tenantId = tenantId;
        On<EngagementManagementAcknowledged>(Apply);
        On<ServiceEngagementAcceptanceRecorded>(Apply);
        On<ServiceEngagementStaffAssigned>(Apply);
        On<DirectoryIndependenceReevaluated>(Apply);
        On<PartnerIndependenceEvaluationRecorded>(Apply);
    }

    public long Sequence { get; private set; }

    public ServiceEngagementView? Engagement(Uuid engagementId) => _engagements.GetValueOrDefault(engagementId);
    public IReadOnlyList<ServiceEngagementView> Engagements => Array.AsReadOnly(_engagements.Values
        .OrderBy(engagement => engagement.EngagementId.ToString(), StringComparer.Ordinal).ToArray());
    public IReadOnlyList<ServiceEngagementView> EngagementHistory(Uuid engagementId) =>
        Array.AsReadOnly(_engagementHistory.GetValueOrDefault(engagementId)?.ToArray() ?? []);

    public IndependenceHistoryView History() => new(_tenantId, Sequence, [], Array.AsReadOnly(_services.ToArray()), [])
    {
        DirectoryReevaluations = Array.AsReadOnly(_directoryReevaluations.Select(FreezeDirectoryReevaluation).ToArray()),
        PartnerEvaluations = Array.AsReadOnly(_partnerEvaluations.ToArray())
    };

    static string AcceptanceDigest(ServiceEngagementAcceptanceView acceptance) =>
        IndependenceSourceDigest.Acceptance(acceptance);

    static ServiceEngagementAcceptanceView FreezeAcceptedSnapshot(ServiceEngagementAcceptanceView acceptance) => acceptance with
    {
        Rules = IndependenceRecordValidation.Freeze(acceptance.Rules),
        PartnerEvaluation = FreezePartnerEvaluation(acceptance.PartnerEvaluation),
        CompleteServiceHistory = Array.AsReadOnly(acceptance.CompleteServiceHistory.Select(IndependenceRecordValidation.Freeze).ToArray()),
        Assignments = Array.AsReadOnly(acceptance.Assignments.ToArray()),
        ConsideredServiceRecordIds = Array.AsReadOnly(acceptance.ConsideredServiceRecordIds.ToArray())
    };

    void Fence(Uuid tenantId, long expectedSequence)
    {
        if (tenantId != _tenantId || expectedSequence != Sequence)
            throw new InvalidOperationException("An independence event must match its owning tenant and sequence.");
        Sequence++;
    }

    void Remember<T>(Uuid requestId, T request, ActorReference actor, object response) =>
        _decisions.Add(requestId, (Intent(request), actor, response));

    Result<T>? Retry<T>(Uuid requestId, object request, ActorReference actor)
    {
        if (!_decisions.TryGetValue(requestId, out var previous))
            return null;
        return previous.Intent == Intent(request) && previous.Actor == actor && previous.Response is T response
            ? Result<T>.Success(response)
            : Failure<T>(RequestErrorKind.Conflict, "The request identity already retains a different independence intent or actor.");
    }

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value,
        value!.GetType(), ComplianceCoreJsonContext.Default).Length <= 48 * 1024;

    bool ValidMutation(Uuid requestId, Uuid engagementId, ActorReference actor, DateTimeOffset recordedAt) =>
        _tenantId != Uuid.Empty && engagementId != Uuid.Empty &&
        IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "member");
    static bool ValidStaff(FirmStaffMemberView staff, string practice) => ServiceEngagementRules.ValidStaff(staff, practice);
    static bool BoundedEngagement(string value) => ServiceEngagementRules.Bounded(value);
    static IndependenceServiceClassification Classification(string value) => IndependenceWire.Classification(value);

    static EngagementPractice Practice(string value) => value switch
    {
        "advisory" => EngagementPractice.Advisory,
        "attest" => EngagementPractice.Attest,
        _ => throw new InvalidOperationException("An engagement practice was invalid."),
    };

    Result<ServiceEngagementView>? AssignmentFailure(Uuid engagementId, FirmStaffMemberView staff)
    {
        var decision = IndependenceCompartments.CanAssign(_assignmentHistory,
            new EngagementAssignment(_tenantId, engagementId, staff.StaffMemberId, staff.UserId, Practice(staff.Practice)));
        return decision.Code switch
        {
            IndependenceDecisionCode.Allowed => null,
            IndependenceDecisionCode.PersonPracticeConflict => Failure<ServiceEngagementView>(RequestErrorKind.Conflict,
                $"Rule {decision.RuleId} refuses the opposite practice. Conflicting historical engagement: {decision.ConflictingAssignment!.EngagementId}; staff: {decision.ConflictingAssignment.FirmStaffMemberId}; user: {decision.ConflictingAssignment.UserId}."),
            _ => Failure<ServiceEngagementView>(RequestErrorKind.Validation,
                "A draft engagement requires its valid period, practice, scope, current matching directory staff and client member attribution."),
        };
    }

    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
