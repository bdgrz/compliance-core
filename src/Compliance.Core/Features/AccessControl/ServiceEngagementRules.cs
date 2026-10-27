using Bdgrz.Compliance.Features.AccessControl.Models;
using System.Collections.ObjectModel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The pure rules of the engagement draft lifecycle: they derive the view after a change from the view before it,
///     the directory staff snapshot a command relied on and the attributed decision. Nothing here reads a stream.
/// </summary>
static class ServiceEngagementRules
{
    public static Result<ServiceEngagementView> Create(Uuid tenantId, Uuid engagementId,
        ServiceEngagementDraftContent content, FirmStaffMemberView lead, ActorReference actor, DateTimeOffset at) =>
        !ValidContent(content) || !ValidStaff(lead, content.Practice) ||
        lead.StaffMemberId != content.EngagementLeadStaffMemberId
            ? Invalid()
            : Result<ServiceEngagementView>.Success(new ServiceEngagementView(tenantId, engagementId, 1, content,
                "draft", Array.AsReadOnly(new[] { Proposed(lead, actor, at) }), actor, at, false));

    public static Result<ServiceEngagementView> Amend(ServiceEngagementView current,
        ServiceEngagementDraftContent content, FirmStaffMemberView lead, ActorReference actor, DateTimeOffset at)
    {
        if (!ValidContent(content) || !ValidStaff(lead, content.Practice) ||
            lead.StaffMemberId != content.EngagementLeadStaffMemberId)
            return Invalid();
        if (content.Practice != current.Content.Practice)
            return Failure(RequestErrorKind.Conflict, "An engagement amendment cannot rewrite its practice identity.");
        return Result<ServiceEngagementView>.Success(current with
        {
            Revision = current.Revision + 1,
            Content = content,
            Staff = Replace(current.Staff, lead, actor, at),
            Actor = actor,
            RecordedAt = at
        });
    }

    public static Result<ServiceEngagementView> Propose(ServiceEngagementView current, FirmStaffMemberView staff,
        ActorReference actor, DateTimeOffset at) => !ValidStaff(staff, current.Content.Practice)
        ? Invalid()
        : Result<ServiceEngagementView>.Success(current with
        {
            Revision = current.Revision + 1,
            Staff = Replace(current.Staff, staff, actor, at),
            Actor = actor,
            RecordedAt = at
        });

    public static Result<ServiceEngagementView> Withdraw(ServiceEngagementView current, Uuid staffMemberId,
        ActorReference actor, DateTimeOffset at)
    {
        if (current.Content.EngagementLeadStaffMemberId == staffMemberId)
            return Failure(RequestErrorKind.Conflict, "Amend the lead before withdrawing their proposal.");
        if (!current.Staff.Any(staff => staff.StaffMemberId == staffMemberId && staff.IsCurrent))
            return Failure(RequestErrorKind.NotFound, "The current tentative staff proposal was not found.");
        return Result<ServiceEngagementView>.Success(current with
        {
            Revision = current.Revision + 1,
            Staff = Array.AsReadOnly(current.Staff.Select(staff => staff.StaffMemberId == staffMemberId
                ? staff with { ProposalState = "withdrawn", IsCurrent = false, Actor = actor, RecordedAt = at }
                : staff).ToArray()),
            Actor = actor,
            RecordedAt = at
        });
    }

    public static ServiceEngagementView Close(ServiceEngagementView current, ActorReference actor,
        DateTimeOffset at) => current with
        {
            Revision = current.Revision + 1,
            Status = "closed",
            Staff = Array.AsReadOnly(current.Staff
                .Select(staff => staff with { ProposalState = "withdrawn", IsCurrent = false }).ToArray()),
            Actor = actor,
            RecordedAt = at
        };

    /// <summary>
    ///     Revises the accepted snapshot after an actual assignment is revoked. Revoking the lead, or naming no one,
    ///     closes the whole acceptance; the removed assignments stay as permanent history.
    /// </summary>
    public static ServiceEngagementAcceptanceView Revoke(ServiceEngagementAcceptanceView acceptance, Uuid leadStaffMemberId,
        Uuid? staffMemberId, string reason, ActorReference actor, DateTimeOffset at)
    {
        var closes = staffMemberId is null || staffMemberId == leadStaffMemberId;
        return acceptance with
        {
            Revision = acceptance.Revision + 1,
            Status = closes ? "closed" : "active",
            ChangedBy = actor,
            ChangedAt = at,
            ChangeReason = reason,
            Assignments = Array.AsReadOnly(acceptance.Assignments.Select(staff =>
                closes || staff.StaffMemberId == staffMemberId ? staff with { IsCurrent = false } : staff).ToArray())
        };
    }

    public static bool ValidContent(ServiceEngagementDraftContent? content) => content is not null &&
        content.Practice is "advisory" or "attest" && Bounded(content.Scope) &&
        content.PeriodStart != DateOnly.MinValue && (content.PeriodEnd is null || content.PeriodEnd >= content.PeriodStart) &&
        content.EngagementLeadStaffMemberId != Uuid.Empty &&
        (content.ExaminationBoundary is null || content.ExaminationBoundary.BoundaryId != Uuid.Empty &&
            content.ExaminationBoundary.VersionId != Uuid.Empty && content.ExaminationBoundary.Revision > 0);

    public static bool ValidStaff(FirmStaffMemberView? staff, string practice) => staff is not null &&
        staff.IsActive && staff.StaffMemberId != Uuid.Empty && staff.UserId != Uuid.Empty &&
        staff.Revision > 0 && staff.Practice == practice;

    public static EngagementPractice PracticeOf(string practice) => practice switch
    {
        "advisory" => EngagementPractice.Advisory,
        "attest" => EngagementPractice.Attest,
        _ => throw new InvalidOperationException("An engagement practice was invalid."),
    };

    public static bool Bounded(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2000 &&
        !value.Any(char.IsControl) && value == value.Trim();

    public static ServiceEngagementStaffProposalView Proposed(FirmStaffMemberView staff, ActorReference actor,
        DateTimeOffset at) => new(staff.StaffMemberId, staff.UserId, staff.Practice, staff.Revision, "proposed", true,
        actor, at, false);

    static ReadOnlyCollection<ServiceEngagementStaffProposalView> Replace(
        IReadOnlyList<ServiceEngagementStaffProposalView> existing, FirmStaffMemberView staff, ActorReference actor,
        DateTimeOffset at) => Array.AsReadOnly(existing.Where(item => item.StaffMemberId != staff.StaffMemberId)
        .Append(Proposed(staff, actor, at))
        .OrderBy(item => item.StaffMemberId.ToString(), StringComparer.Ordinal).ToArray());

    static Result<ServiceEngagementView> Invalid() => Failure(RequestErrorKind.Validation,
        "A draft engagement requires its valid period, practice, scope, current matching directory staff and client member attribution.");

    static Result<ServiceEngagementView> Failure(RequestErrorKind kind, string message) =>
        Result<ServiceEngagementView>.Failure(new RequestError(kind, message));
}
