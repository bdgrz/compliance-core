using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     The pure derivation of the accepted snapshot of an engagement: the complete original view an acceptance records
///     and the revised view after one more actual assignment. The server-frozen decision instant is the instant of
///     record, so every source time of the snapshot can be checked against it.
/// </summary>
static class EngagementAcceptanceViews
{
    /// <summary>The acceptance view of an engagement accepted at <paramref name="at" /> by the partner <paramref name="actor" />.</summary>
    public static ServiceEngagementAcceptanceView Accepted(EngagementAcceptanceEvidence evidence, ActorReference actor,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(actor);
        var proof = evidence.Proof ?? throw new InvalidOperationException("An acceptance relies on verified evidence.");
        var rules = evidence.Rules ?? throw new InvalidOperationException("An acceptance relies on ratified rules.");
        var proposed = evidence.Engagement.Staff.Where(staff => staff.IsCurrent).ToArray();
        var assignments = proposed.Select(staff => new EngagementActualAssignmentView(staff.StaffMemberId,
            staff.UserId, staff.Practice, staff.DirectoryStaffRevision,
            proof.CurrentStaff.Single(current => current.StaffMemberId == staff.StaffMemberId).RecordedAt, true,
            actor, at, DateOnly.FromDateTime(at.UtcDateTime))).ToArray();
        return new ServiceEngagementAcceptanceView(evidence.Engagement.TenantId, evidence.Engagement.EngagementId,
            evidence.Engagement.Revision, 1, proof.ReviewTaskId, proof.PartnerStaffMemberId, proof.PartnerUserId,
            proof.CurrentPartner.Revision, proof.PartnerDutyRevision, proof.AuthorityReference,
            proof.PartnerEvaluationReference, proof.ManagementAcknowledgementId, proof.Boundary?.BoundaryId,
            proof.Boundary?.VersionId, proof.Boundary?.Revision, proof.BoundaryApproval?.DecisionId,
            new EngagementAcceptanceSourceTimes(proof.CurrentPartner.RecordedAt, proof.PartnerAuthorityVerifiedAt,
                proof.Boundary?.ChangedAt, proof.BoundaryApproval?.DecidedAt), rules, evidence.Services,
            evidence.DecisionCode, evidence.EvaluationOutcome, evidence.ConsideredServiceRecordIds,
            Array.AsReadOnly(assignments), "active", actor, at)
        {
            PartnerEvaluation = proof.PartnerEvaluation,
        };
    }

    /// <summary>The acceptance view after <paramref name="staff" /> was actually assigned at <paramref name="at" />.</summary>
    public static ServiceEngagementAcceptanceView Assigned(ServiceEngagementAcceptanceView current,
        FirmStaffMemberView staff, DateOnly effectiveFrom, ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(staff);
        ArgumentNullException.ThrowIfNull(actor);
        var assignment = new EngagementActualAssignmentView(staff.StaffMemberId, staff.UserId, staff.Practice,
            staff.Revision, staff.RecordedAt, true, actor, at, effectiveFrom);
        return current with
        {
            Revision = current.Revision + 1,
            Assignments = Array.AsReadOnly(current.Assignments.Append(assignment).ToArray()),
            ChangedBy = actor,
            ChangedAt = at,
        };
    }

    /// <summary>The wire outcome of an acceptance policy decision.</summary>
    public static string OutcomeOf(IndependenceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return decision.EvaluationOutcome switch
        {
            IndependenceEvaluationOutcome.Compatible => "compatible",
            IndependenceEvaluationOutcome.ConditionallyCompatible => "conditionally_compatible",
            _ => "not_applicable",
        };
    }
}
