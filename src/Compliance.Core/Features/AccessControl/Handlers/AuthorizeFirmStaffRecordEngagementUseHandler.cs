using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
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
