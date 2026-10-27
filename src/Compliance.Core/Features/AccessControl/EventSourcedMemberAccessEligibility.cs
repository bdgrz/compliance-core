using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Checks member events before the membership projection can catch up.</summary>
sealed class EventSourcedMemberAccessEligibility(IAggregateReader reader,
    TimeProvider? clock = null) : IMemberAccessEligibility
{
    readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            return false;
        try
        {
            var member = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
            return IsEligible(member);
        }
        catch (MemberHistoryIntegrityException)
        {
            return false;
        }
    }

    public async ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty)
            return null;
        try
        {
            var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
                .ConfigureAwait(false);
            return IsEligible(member) ? member.MembershipEpisodeId : null;
        }
        catch (MemberHistoryIntegrityException)
        {
            return null;
        }
    }

    bool IsEligible(Member member) => member.IsRegistered &&
        (member.Affiliation is "client_personnel" or "guest") &&
        member.IsAccessEligibleOn(DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime));
}
