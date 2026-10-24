using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Builds the guard's compact contribution set from an authoritative source cut: every administrators-team
///     assignment read from the team-member streams, paired with the exact member episode and state of its member.
///     It runs only while the tenant's guard stream is empty; the first guard fact is appended at physical position
///     zero, so any concurrent admission that wins the append fences the cut and forces a fresh read. No directory
///     projection or boolean takes part.
/// </summary>
public sealed class TenantManagerGuardBootstrap(IDomainEventReader events, IAggregateReader reader, TimeProvider clock)
{
    /// <summary>Reads the cut and returns the contributions it proves.</summary>
    public async Task<GuardBaseline> BuildAsync(Uuid tenantId, CancellationToken ct)
    {
        var team = BuiltInRbac.AdministratorsTeamId(tenantId);
        var assignments = new Dictionary<Uuid, Assignment>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-team-members");
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            switch (record.Event)
            {
                case TeamMemberAssigned assigned when assigned.TeamId == team && assigned.TenantId == tenantId:
                    assignments[assigned.MemberId] = new Assignment(true, assigned.MembershipEpisodeId,
                        assignments.GetValueOrDefault(assigned.MemberId)?.Generation + 1 ?? 1);
                    break;
                case TeamMemberRemoved removed when removed.TeamId == team && removed.TenantId == tenantId &&
                                                   assignments.TryGetValue(removed.MemberId, out var current):
                    assignments[removed.MemberId] = current with { IsAssigned = false };
                    break;
            }
        }

        var contributions = new List<GuardBaselineContribution>();
        foreach (var (memberId, assignment) in assignments.Where(static pair => pair.Value.IsAssigned)
                     .OrderBy(static pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct).ConfigureAwait(false);
            if (member.MembershipEpisodeId != assignment.Episode || !member.IsRegistered && !member.IsDeprovisioned)
                continue;
            var reasons = new List<string>();
            if (member.IsDeprovisioned)
                reasons.Add(TenantManagerGuard.Deprovisioned);
            else if (member.IsSuspended)
                reasons.Add(TenantManagerGuard.Suspended);
            contributions.Add(new GuardBaselineContribution(memberId, assignment.Episode,
                RbacIds.TeamMember(tenantId, team, memberId), assignment.Generation, reasons));
        }

        return new GuardBaseline(contributions, clock.GetUtcNow());
    }

    sealed record Assignment(bool IsAssigned, Uuid Episode, long Generation);
}
