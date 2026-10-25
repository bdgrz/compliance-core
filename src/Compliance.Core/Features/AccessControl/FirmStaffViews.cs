using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Authoritative point reads of staff records: by staff member through the record stream, by canonical user
///     through the permanent claim. A pending, rejected or unknown registration has no view.
/// </summary>
public sealed class FirmStaffViews(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
{
    /// <summary>The current view of one registered staff member, or null.</summary>
    public async ValueTask<FirmStaffMemberView?> GetAsync(Uuid staffMemberId, CancellationToken ct = default)
    {
        if (staffMemberId == Uuid.Empty)
            return null;
        var record = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId), ct).ConfigureAwait(false);
        return record.Latest is { } latest
            ? await ReadAsync(latest, ct).ConfigureAwait(false)
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
    public async Task<FirmStaffMemberView> ReadAsync(FirmStaffChangeRecorded change, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!Uuid.TryParse(change.Operation.Owner.Resource, null, out var staffMemberId) ||
            change.Operation.Owner != new FirmStaffRecord(staffMemberId).Stream)
            throw new ImmutableContentIntegrityException("The staff change names another record stream.");
        var owner = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId), ct).ConfigureAwait(false);
        var history = await FirmStaffHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (!history.Matches(change))
            throw new ImmutableContentIntegrityException("The staff view requires an exact verified historical staff decision.");
        var intent = await FirmStaffContent.ReadIntentAsync(content, change.Intent, ct).ConfigureAwait(false);
        FirmStaffContent.RequireIntent(owner, intent);
        if (owner.RegistrationIntent != change.Intent)
            throw new ImmutableContentIntegrityException("The staff change does not use its registered immutable identity.");
        var view = await FirmStaffContent.ReadStaffAsync(content, change.StaffContent, ct).ConfigureAwait(false);
        var registered = change.ChangeKind == "register";
        if (view.StaffMemberId != staffMemberId || view.UserId != intent.CanonicalUserId ||
            view.Practice != intent.Practice || view.SourceReference != intent.SourceReference ||
            view.Revision != (long)change.Revision ||
            view.Actor != (registered ? intent.Header.OriginalActor : change.Operation.Actor) ||
            view.RecordedAt != (registered ? intent.Header.AcceptedAt : change.Operation.AcceptedAt) ||
            registered && (!view.IsActive || change.Revision != 1))
            throw new ImmutableContentIntegrityException("The verified staff body differs from its exact native decision and registration identity.");
        if (change.OperationResult is not { } resultRef)
            throw new ImmutableContentIntegrityException("The staff decision has no original typed mutation result.");
        var result = await content.ReadJsonAsync<MutationResultContent>(FirmStaffContent.Realm, resultRef, ct)
            .ConfigureAwait(false);
        if (result.Schema != FirmStaffContent.StaffSchema || result.Value is not StandardResponseBody body ||
            body.Content != change.StaffContent)
            throw new ImmutableContentIntegrityException("The staff result does not reference its exact recorded staff body.");
        return view;
    }
}
