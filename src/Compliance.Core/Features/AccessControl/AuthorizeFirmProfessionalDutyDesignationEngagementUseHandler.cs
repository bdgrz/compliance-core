using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The duty-designation pair of the generic acquire receiver: it holds the exact active engagement-partner designation a partner evaluation pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeFirmProfessionalDutyDesignationEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementPersonalDecisionVerifier verifier)
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
