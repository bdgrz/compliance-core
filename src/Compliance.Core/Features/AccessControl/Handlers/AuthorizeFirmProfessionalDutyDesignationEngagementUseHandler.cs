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
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
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
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The duty-designation pair of the generic acquire receiver: it holds the exact active engagement-partner designation a partner evaluation pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeFirmProfessionalDutyDesignationEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseAuthorizeHandler<FirmProfessionalDutyDesignation, AuthorizeFirmProfessionalDutyDesignationEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse);

    protected override FirmProfessionalDutyDesignation Open(AuthorizeFirmProfessionalDutyDesignationEngagementUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, FirmProfessionalDuty.EngagementPartner,
            EngagementPersonalDecisionVerifier.TenantOf(request.Source.Stream), selectedCommandId);

    protected override async ValueTask<Result> VerifyPinnedAsync(FirmProfessionalDutyDesignation owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (owner.Latest is null)
            return Waiting("The designation is not visible yet.");
        if (!IsPinned(owner, requirement))
            return Stale("The designation changed after the decision was admitted.");
        FirmProfessionalDutyDesignationView designation;
        try
        {
            designation = await ProfessionalDutyContent.ReadDesignationAsync(Content, requirement.RequiredFacts, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Stale($"The pinned designation content cannot be verified: {exception.Message}");
        }

        return designation is { IsActive: true, AuthorityStatus: "active", Duty: FirmProfessionalDuty.EngagementPartner } &&
            designation.UserId == owner.UserId && designation.TenantId == owner.TenantId
            ? Result.Success
            : Stale("The partner designation is not active for this client.");
    }

    protected override bool IsPinned(FirmProfessionalDutyDesignation owner, SourceUseRequirement requirement) =>
        (owner.Latest as DomainEvent)?.Metadata.EventId == requirement.ContentEvent.EventId &&
        owner.Revision == requirement.ResourceRevision;

    protected override Func<FirmProfessionalDutyDesignation, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (FirmProfessionalDutyDesignation owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizeEngagementUse(provenance, intent, generation);
}
