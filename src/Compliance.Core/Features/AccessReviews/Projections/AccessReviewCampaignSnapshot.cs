using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Projections;

/// <summary>
///     The composed read of one campaign: its campaign stream plus the state of every frozen item read from the
///     item streams. It answers the questions the single campaign aggregate answered before the split; the logical
///     <see cref="Revision" /> is the campaign's own, and each item carries its own revision.
/// </summary>
public sealed class AccessReviewCampaignSnapshot
{
    readonly Dictionary<Uuid, AccessReviewItemState> _items;
    readonly List<Uuid> _order;

    internal AccessReviewCampaignSnapshot(Uuid tenantId, AccessReviewCampaign campaign, AccessReviewLaunch? launched,
        AccessReviewCampaignCompletionView? completion, IReadOnlyList<AccessReviewItemState> items,
        AccessReviewCampaignHistorySnapshot history)
    {
        TenantId = tenantId;
        Campaign = campaign;
        History = history;
        Launched = launched;
        Completion = completion;
        _order = [.. items.Select(static item => item.Item.ItemId)];
        _items = items.ToDictionary(static item => item.Item.ItemId);
    }

    /// <summary>The tenant.</summary>
    public Uuid TenantId { get; }

    /// <summary>The campaign stream's aggregate.</summary>
    public AccessReviewCampaign Campaign { get; }

    public AccessReviewCampaignHistorySnapshot History { get; }

    /// <summary>The campaign id.</summary>
    public Uuid Id => Campaign.Id;

    /// <summary>The frozen launch, or null when the campaign was not launched.</summary>
    public AccessReviewLaunch? Launched { get; }

    /// <summary>The final completion, or null while the campaign is active.</summary>
    public AccessReviewCampaignCompletionView? Completion { get; }

    /// <summary>The campaign's own logical revision.</summary>
    public long Revision => Campaign.Revision;

    /// <summary>True once the campaign was launched.</summary>
    public bool IsLaunched => Launched is not null;

    /// <summary>The campaign status.</summary>
    public string Status => Completion is null ? AccessReviewCampaign.Active : AccessReviewCampaign.Completed;

    /// <summary>The state of every item in launch order.</summary>
    public IEnumerable<AccessReviewItemState> Items => _order.Select(itemId => _items[itemId]);

    /// <summary>The frozen item, or null when the campaign has no such item.</summary>
    public AccessReviewItemView? FindItem(Uuid itemId) => _items.TryGetValue(itemId, out var state) ? state.Item : null;

    /// <summary>The state of an item, or null when the campaign has no such item.</summary>
    public AccessReviewItemState? State(Uuid itemId) => _items.GetValueOrDefault(itemId);

    /// <summary>The current decision of an item.</summary>
    public AccessDecisionView? CurrentDecision(Uuid itemId) => State(itemId)?.CurrentDecision;

    /// <summary>True when the member currently reviews at least one item.</summary>
    public bool IsReviewer(Uuid memberId) => _items.Values.Any(state => state.CurrentReviewerMemberId == memberId);

    /// <summary>The current reviewer of an item.</summary>
    public Uuid? CurrentReviewerMemberId(Uuid itemId) => State(itemId)?.CurrentReviewerMemberId;

    /// <summary>The current remediation owner of an item.</summary>
    public Uuid? CurrentRemediationOwnerMemberId(Uuid itemId) => State(itemId)?.CurrentRemediationOwnerMemberId;

    /// <summary>Explains why the reviewer cannot decide an item, or null when they can.</summary>
    public string? Eligibility(Uuid itemId, Uuid reviewerMemberId, Uuid reviewerUserId, bool bulk) =>
        State(itemId) is { } state ? state.Eligibility(reviewerMemberId, reviewerUserId, bulk) : "not_found";

    /// <summary>Lists the reasons the campaign cannot complete at <paramref name="now" />.</summary>
    public IReadOnlyList<string> CompletionBlockers(DateTimeOffset now) => [.. Items
        .Select(item => item.CompletionBlocker(now)).OfType<string>()];

    /// <summary>The state views of the items the predicate admits.</summary>
    public IReadOnlyList<AccessReviewItemStateView> ItemStates(DateTimeOffset now,
        Func<AccessReviewItemView, bool>? visible = null) => [.. Items
        .Where(state => visible is null || visible(state.Item)).Select(state => state.ToView(now))];

    /// <summary>The public view of the campaign as of <paramref name="now" />.</summary>
    public AccessReviewCampaignView ToView(DateTimeOffset now, Func<AccessReviewItemView, bool>? visible = null)
    {
        var launched = Launched ?? throw new InvalidOperationException("The campaign was not launched.");
        var items = ItemStates(now, visible);
        return new AccessReviewCampaignView(TenantId, Id, Revision, launched.Name, launched.Instructions,
            launched.Deadline, Status, launched.SnapshotId, launched.ContentSha256,
            [.. launched.Reviewers.Where(reviewer => visible is null ||
                items.Any(item => item.Item.PopulationId == reviewer.PopulationId))],
            items, items.Count(static item => item.Status == "unresolved"),
            items.Count(static item => item.RemediationStatus is "pending" or "provider_changed"),
            launched.LaunchedBy, launched.LaunchedAt, Completion, launched.ProgramId, launched.RemediationOwnerMemberId);
    }
}
