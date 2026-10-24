using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Authoritative point reads of staff records: by staff member through the record stream, by canonical user
///     through the permanent claim. A pending, rejected or unknown registration has no view.
/// </summary>
public sealed class FirmStaffViews(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>The current view of one registered staff member, or null.</summary>
    public async ValueTask<FirmStaffMemberView?> GetAsync(Uuid staffMemberId, CancellationToken ct = default)
    {
        if (staffMemberId == Uuid.Empty)
            return null;
        var record = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId), ct).ConfigureAwait(false);
        return record.Latest is { } latest
            ? await FirmStaffContent.ReadStaffAsync(content, latest.StaffContent, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>The current view of the staff member who permanently claimed a canonical user, or null.</summary>
    public async ValueTask<FirmStaffMemberView?> FindByUserAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return null;
        var claim = await reader.HydrateAsync(new FirmStaffIdentityClaim(userId), ct).ConfigureAwait(false);
        if (claim.ClaimedByStaffMemberId is not { } staffMemberId)
            return null;
        var staff = await GetAsync(staffMemberId, ct).ConfigureAwait(false);
        return staff is not null && staff.UserId == userId ? staff : null;
    }

    /// <summary>The complete staff view a retained change fact recorded.</summary>
    public Task<FirmStaffMemberView> ReadAsync(FirmStaffChangeRecorded change, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        return FirmStaffContent.ReadStaffAsync(content, change.StaffContent, ct);
    }
}
