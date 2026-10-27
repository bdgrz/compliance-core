using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

/// <summary>Reads the exact verified immutable content selected by the contribution authority.</summary>
public sealed class MethodologyTemplateContributionReader(IAggregateReader reader, IImmutableContentStore content)
{
    public const string Schema = "methodology-template-contribution.v1";

    public async ValueTask<MethodologyTemplateContributionView?> ReadAsync(Uuid tenantId, Uuid contributionId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new MethodologyTemplateContribution(tenantId, contributionId), ct)
            .ConfigureAwait(false);
        return await ReadAsync(owner, tenantId, contributionId, content, ct).ConfigureAwait(false);
    }

    internal static async ValueTask<MethodologyTemplateContributionView?> ReadAsync(MethodologyTemplateContribution owner,
        Uuid tenantId, Uuid contributionId, IImmutableContentStore content, CancellationToken ct)
    {
        if (owner.Content is not { } reference)
            return null;
        if (reference.Schema != Schema)
            throw new ImmutableContentIntegrityException("The contribution content uses another schema.");
        var view = await content.ReadJsonAsync<MethodologyTemplateContributionView>(tenantId.ToString(), reference, ct)
            .ConfigureAwait(false);
        if (view.TenantId != tenantId || view.ContributionId != contributionId || view.Revision != owner.Revision ||
            view.Status != owner.Status || view.UpdatedAt != owner.UpdatedAt ||
            view.ProposedByMemberId != RbacIds.Member(tenantId, owner.ProposerUserId) ||
            view.Review is { } review && (owner.ReviewerUserId is not { } reviewerUser ||
                review.ReviewedByMemberId != RbacIds.Member(Uuid.Empty, reviewerUser)) ||
            view.ActorMemberId != RbacIds.Member(owner.Status == "proposed" ? tenantId : Uuid.Empty, owner.LastActingUserId) ||
            !new MethodologyTemplateContributionRules(tenantId, contributionId, view).HasValidCurrent)
            throw new ImmutableContentIntegrityException("Contribution content differs from its exact authoritative revision.");
        return view;
    }

    public async ValueTask<MethodologyTemplateContributionShareView?> ShareAsync(Uuid tenantId, Uuid contributionId,
        CancellationToken ct = default)
    {
        var view = await ReadAsync(tenantId, contributionId, ct).ConfigureAwait(false);
        return view is null ? null : new MethodologyTemplateContributionRules(tenantId, contributionId, view).ShareableContent;
    }
}
