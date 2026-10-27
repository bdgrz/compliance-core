using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     The permanent person-practice wall, read from the practice commitment owners. A person is both a staff member
///     and a canonical user, and a commitment of either subject binds the person: nobody who ever served one practice
///     for a client can serve the opposite one. Every read hydrates only the subjects it asks about, so no
///     client-wide history is read and no count bounds the wall.
/// </summary>
public sealed class PracticeIndependenceWall(IAggregateReader reader, IDomainEventReader events)
{
    /// <summary>
    ///     Decides whether a candidate assignment is allowed: it is refused when the staff member or the canonical
    ///     user permanently serves the opposite practice for the client.
    /// </summary>
    public async ValueTask<IndependenceDecision> CanAssignAsync(Uuid tenantId, EngagementAssignment candidate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (tenantId == Uuid.Empty || candidate.ClientTenantId != tenantId || candidate.EngagementId == Uuid.Empty ||
            candidate.FirmStaffMemberId == Uuid.Empty || candidate.UserId == Uuid.Empty ||
            !Enum.IsDefined(candidate.Practice))
            return new IndependenceDecision(IndependenceDecisionCode.AssignmentInvalid);
        var practice = EngagementAcceptanceEffects.PracticeOf(candidate.Practice);
        var staff = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId, PracticeSubjectKind.Staff,
            candidate.FirmStaffMemberId), ct).ConfigureAwait(false);
        var user = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId,
            PracticeSubjectKind.CanonicalUser, candidate.UserId), ct).ConfigureAwait(false);
        var conflicting = new[] { staff, user }.FirstOrDefault(subject => subject.Practice is { } served &&
            served != practice);
        return conflicting is null
            ? IndependenceDecision.Allow
            : new IndependenceDecision(IndependenceDecisionCode.PersonPracticeConflict,
                ruleId: IndependenceCompartments.PersonExclusivityRuleId,
                conflictingAssignment: new EngagementAssignment(tenantId,
                    conflicting.FirstCommitment!.EngagementId, candidate.FirmStaffMemberId, candidate.UserId,
                    ServiceEngagementRules.PracticeOf(conflicting.Practice!)));
    }

    /// <summary>
    ///     The permanent assignments of a canonical user for a client, one per commitment. Only the practice and the
    ///     client are meaningful to the denial-only guards that read them.
    /// </summary>
    public async ValueTask<IReadOnlyList<EngagementAssignment>> AssignmentsOfUserAsync(Uuid tenantId, Uuid userId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            return [];
        var user = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId,
            PracticeSubjectKind.CanonicalUser, userId), ct).ConfigureAwait(false);
        var history = await PracticeCommitmentHistory.ReadAsync(events, user, ct).ConfigureAwait(false);
        return history.Assignments();
    }

    /// <summary>True when the canonical user permanently serves exactly this practice for the client.</summary>
    public async ValueTask<bool> ServesAsync(Uuid tenantId, Uuid userId, EngagementPractice practice,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            return false;
        var user = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId,
            PracticeSubjectKind.CanonicalUser, userId), ct).ConfigureAwait(false);
        return user.Serves(EngagementAcceptanceEffects.PracticeOf(practice));
    }
}
