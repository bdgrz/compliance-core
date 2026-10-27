using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The staff-record pair of the generic acquire receiver: it holds the exact registered, currently active staff content a partner evaluation pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeFirmStaffRecordEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseAuthorizeHandler<FirmStaffRecord, AuthorizeFirmStaffRecordEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(AuthorizeFirmStaffRecordEngagementUse);

    protected override FirmStaffRecord Open(AuthorizeFirmStaffRecordEngagementUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result> VerifyPinnedAsync(FirmStaffRecord owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (!owner.IsRegistered)
            return Waiting("The staff registration is not visible yet.");
        if (!IsPinned(owner, requirement))
            return Stale("The staff record changed after the decision was admitted.");
        FirmStaffMemberView staff;
        try
        {
            staff = await FirmStaffContent.ReadStaffAsync(Content, requirement.RequiredFacts, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Stale($"The pinned staff content cannot be verified: {exception.Message}");
        }

        return staff.IsActive && staff.StaffMemberId == owner.StaffMemberId
            ? Result.Success
            : Stale("The partner is not active firm staff.");
    }

    protected override bool IsPinned(FirmStaffRecord owner, SourceUseRequirement requirement) =>
        owner.Latest is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        owner.Revision == requirement.ResourceRevision;

    protected override Func<FirmStaffRecord, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (FirmStaffRecord owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizeEngagementUse(provenance, intent, generation);
}
