using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed partial class IndependenceLedger
{
    readonly HashSet<Uuid> _actualAssignmentRequests = [];

    internal Result<ServiceEngagementAcceptanceView>? RetryActualAssignment(Uuid requestId,
        AssignServiceEngagementStaff request, ActorReference actor) =>
        _actualAssignmentRequests.Contains(requestId)
            ? Retry<ServiceEngagementAcceptanceView>(requestId, request, actor)
            : null;

    internal Result<ServiceEngagementAcceptanceView> AssignActualStaff(Uuid requestId,
        AssignServiceEngagementStaff request, FirmStaffMemberView staff,
        CurrentProfessionalDuty currentPartner, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (request is null || currentPartner is null || staff is null ||
            request.TenantId != _tenantId || request.TenantId == Uuid.Empty ||
            request.EngagementId == Uuid.Empty || request.StaffMemberId == Uuid.Empty ||
            !IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "firm_staff") ||
            !ValidAssignmentPartner(currentPartner, actor, recordedAt))
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Validation,
                "An actual assignment requires a personal current partner, exact client and attributed decision.");

        if (Retry<ServiceEngagementAcceptanceView>(requestId, request, actor) is { } retry)
            return retry;

        if (request.ExpectedSequence != Sequence ||
            Engagement(request.EngagementId) is not { Status: "accepted" } engagement ||
            Acceptance(request.EngagementId) is not { Status: "active" } acceptance ||
            _engagementHistory[request.EngagementId].Count >= 100 ||
            _acceptanceHistory[request.EngagementId].Count >= 100 ||
            acceptance.Assignments.Count >= 100 || _assignmentHistory.Count >= 1000)
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Conflict,
                "Reload the client sequence; only an active accepted engagement with bounded assignment history can change.");

        if (!ValidAssignmentStaff(staff, engagement.Content.Practice, recordedAt) ||
            request.StaffMemberId != staff.StaffMemberId ||
            request.EffectiveFrom < DateOnly.FromDateTime(recordedAt.UtcDateTime) ||
            request.EffectiveFrom < engagement.Content.PeriodStart ||
            engagement.Content.PeriodEnd is { } end && request.EffectiveFrom > end)
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Validation,
                "The assignment requires one current active practice-matched directory row and a non-backdated effective date inside the engagement period.");

        if (acceptance.Assignments.Any(item => item.StaffMemberId == staff.StaffMemberId || item.UserId == staff.UserId))
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Conflict,
                "A staff identity can be assigned to this accepted engagement only once; removed assignments remain permanent history.");

        if (IndependenceCompartments.CanAssign(_assignmentHistory, new EngagementAssignment(_tenantId,
                request.EngagementId, staff.StaffMemberId, staff.UserId, Practice(engagement.Content.Practice)))
            is { Code: not IndependenceDecisionCode.Allowed } decision)
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Conflict,
                $"Rule {decision.RuleId ?? IndependenceCompartments.PersonExclusivityRuleId} refuses this assignment because the person's complete client history is incompatible.");

        if (recordedAt < engagement.RecordedAt || recordedAt < acceptance.RecordedAt ||
            _acceptanceHistory.Values.SelectMany(history => history)
                .Any(item => (item.ChangedAt ?? item.RecordedAt) > recordedAt) ||
            _services.Any(item => item.RecordedAt > recordedAt))
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Conflict,
                "The complete client assignment history changed after the requested decision time.");

        var intent = Intent(request);
        var assigned = new ServiceEngagementStaffAssigned(_tenantId, requestId, request.ExpectedSequence,
            request.EngagementId, staff, request.EffectiveFrom, currentPartner.Staff, currentPartner.Designation,
            currentPartner.ContinuityStartRevision, intent, actor, recordedAt);
        if (!Fits(assigned))
            return Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Validation,
                "The complete staff assignment evidence exceeds the bounded event payload.");

        RaiseEvent(assigned);
        return Result<ServiceEngagementAcceptanceView>.Success(Acceptance(request.EngagementId)!);
    }

    void Apply(ServiceEngagementStaffAssigned ev)
    {
        if (ev.AssignedStaff is null)
            throw new InvalidOperationException("An actual staff assignment must retain its exact directory record.");
        var staff = ev.AssignedStaff;
        var request = new AssignServiceEngagementStaff(ev.TenantId, ev.EngagementId, staff.StaffMemberId,
            ev.ExpectedSequence, ev.EffectiveFrom);
        var engagement = Engagement(ev.EngagementId);
        var acceptance = Acceptance(ev.EngagementId);
        var partner = new CurrentProfessionalDuty(ev.PartnerStaff, ev.PartnerDesignation,
            ev.PartnerDutyContinuityStartRevision);
        if (ev.TenantId != _tenantId || ev.RequestId == Uuid.Empty || ev.ExpectedSequence != Sequence ||
            string.IsNullOrWhiteSpace(ev.Intent) || ev.Intent != Intent(request) ||
            !IndependenceRecordValidation.ValidAttribution(ev.RequestId, ev.Actor, ev.RecordedAt, "firm_staff") ||
            ev.Actor.Id != ev.PartnerStaff.UserId.ToString() || !ValidAssignmentPartner(partner, ev.Actor, ev.RecordedAt) ||
            engagement is not { Status: "accepted" } || acceptance is not { Status: "active" } ||
            engagement.Revision <= 0 || acceptance.Revision <= 0 ||
            _engagementHistory[ev.EngagementId].Count >= 100 || _acceptanceHistory[ev.EngagementId].Count >= 100 ||
            acceptance.Assignments.Count >= 100 || _assignmentHistory.Count >= 1000 ||
            !ValidAssignmentStaff(staff, engagement.Content.Practice, ev.RecordedAt) ||
            ev.EffectiveFrom == DateOnly.MinValue ||
            ev.EffectiveFrom < DateOnly.FromDateTime(ev.RecordedAt.UtcDateTime) ||
            ev.EffectiveFrom < engagement.Content.PeriodStart ||
            engagement.Content.PeriodEnd is { } end && ev.EffectiveFrom > end ||
            ev.RecordedAt < engagement.RecordedAt || ev.RecordedAt < acceptance.RecordedAt ||
            _acceptanceHistory.Values.SelectMany(history => history)
                .Any(item => (item.ChangedAt ?? item.RecordedAt) > ev.RecordedAt) ||
            _services.Any(item => item.RecordedAt > ev.RecordedAt) ||
            acceptance.Assignments.Any(item => item.StaffMemberId == staff.StaffMemberId || item.UserId == staff.UserId) ||
            IndependenceCompartments.CanAssign(_assignmentHistory, new EngagementAssignment(_tenantId,
                ev.EngagementId, staff.StaffMemberId, staff.UserId, Practice(staff.Practice))).Code != IndependenceDecisionCode.Allowed)
            throw new InvalidOperationException("An actual assignment must preserve current partner authority, exact active engagement sources and the permanent person-practice wall.");

        var assignment = new EngagementActualAssignmentView(staff.StaffMemberId, staff.UserId, staff.Practice,
            staff.Revision, staff.RecordedAt, true, ev.Actor, ev.RecordedAt,
            ev.EffectiveFrom);
        var updatedAcceptance = acceptance with
        {
            Revision = acceptance.Revision + 1,
            Assignments = Array.AsReadOnly(acceptance.Assignments.Append(assignment).ToArray()),
            ChangedBy = ev.Actor,
            ChangedAt = ev.RecordedAt
        };
        var updatedEngagement = engagement with
        {
            Revision = engagement.Revision + 1,
            Actor = ev.Actor,
            RecordedAt = ev.RecordedAt
        };

        Fence(ev.TenantId, ev.ExpectedSequence);
        _acceptances[ev.EngagementId] = updatedAcceptance;
        _acceptanceHistory[ev.EngagementId].Add(updatedAcceptance);
        _engagements[ev.EngagementId] = updatedEngagement;
        _engagementHistory[ev.EngagementId].Add(updatedEngagement);
        _assignmentHistory.Add(new EngagementAssignment(_tenantId, ev.EngagementId, staff.StaffMemberId,
            staff.UserId, Practice(staff.Practice)));
        _decisions.Add(ev.RequestId, (ev.Intent, ev.Actor, updatedAcceptance));
        _actualAssignmentRequests.Add(ev.RequestId);
    }

    bool ValidAssignmentPartner(CurrentProfessionalDuty currentPartner, ActorReference actor,
        DateTimeOffset recordedAt)
    {
        var staff = currentPartner.Staff;
        var duty = currentPartner.Designation;
        return staff is { Practice: "advisory" or "attest" } &&
            ValidAssignmentStaff(staff, staff.Practice, recordedAt) && duty is
            { IsActive: true, Duty: FirmProfessionalDuty.EngagementPartner, Revision: > 0 } &&
            duty.TenantId == _tenantId && duty.UserId == staff.UserId &&
            duty.StaffMemberId == staff.StaffMemberId && duty.DirectoryStaffRevision == staff.Revision &&
            currentPartner.ContinuityStartRevision is > 0 && currentPartner.ContinuityStartRevision <= duty.Revision &&
            BoundedEngagement(duty.SourceReference) && staff.RecordedAt <= recordedAt &&
            duty.DesignatedAt <= recordedAt && duty.LastChangedAt <= recordedAt &&
            actor.Kind == "firm_staff" && actor.Id == staff.UserId.ToString();
    }

    static bool ValidAssignmentStaff(FirmStaffMemberView staff, string practice, DateTimeOffset recordedAt) =>
        ValidStaff(staff, practice) && staff.Practice is "advisory" or "attest" &&
        staff.RecordedAt != default && staff.RecordedAt <= recordedAt &&
        BoundedEngagement(staff.SourceReference) && staff.Actor is { Kind: "platform_operator" } &&
        !string.IsNullOrWhiteSpace(staff.Actor.Id);
}
