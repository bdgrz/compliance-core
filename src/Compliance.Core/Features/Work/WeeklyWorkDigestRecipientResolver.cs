using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Checks current member, opt-out, and verified-address source records before delivery.</summary>
public sealed class WeeklyWorkDigestRecipientResolver(IAggregateReader reader,
    IEmailAddressDirectoryReader emailAddresses)
{
    public async ValueTask<string?> SuppressionReasonAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty)
            return "member_inactive";

        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
            member.UserId == Uuid.Empty || RbacIds.Member(tenantId, member.UserId) != memberId)
            return "member_inactive";
        if (member.Affiliation == "guest")
            return "guest_member";

        var preference = await reader.HydrateAsync(new WorkDigestPreference(tenantId, memberId), ct)
            .ConfigureAwait(false);
        if (!preference.Read().EmailDigestEnabled)
            return "email_digest_opted_out";

        return await HasVerifiedAddressAsync(member.UserId, ct).ConfigureAwait(false)
            ? null
            : "verified_email_unavailable";
    }

    async ValueTask<bool> HasVerifiedAddressAsync(Uuid userId, CancellationToken ct)
    {
        string? cursor = null;
        do
        {
            var page = await emailAddresses.ListAsync(userId, 50, cursor, ct).ConfigureAwait(false);
            foreach (var addressView in page.Items)
            {
                if (addressView.UserId != userId || string.IsNullOrWhiteSpace(addressView.EmailAddress))
                    continue;
                var address = await reader.HydrateAsync(new EmailAddress(addressView.EmailAddress), ct)
                    .ConfigureAwait(false);
                if (address.Owner == userId && address.IsVerified)
                    return true;
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return false;
    }
}
