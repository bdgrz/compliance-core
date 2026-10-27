using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     A campaign was launched with its frozen population, reviewers, instructions, and deadline. The reviewers and
///     the complete item list are verified immutable content, so a campaign of any size is one bounded fact.
///     <paramref name="ItemIntentManifest" /> holds the frozen creation intent of every item in canonical item
///     order; the launch fan-out materializes each item from it. The campaign's logical revision is 1 after launch.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.launched", 2)]
public sealed record AccessReviewCampaignLaunched(Uuid TenantId, Uuid CampaignId, string Name,
    string Instructions, DateTimeOffset Deadline, Uuid SnapshotId, string ContentSha256,
    ImmutableContentRef ReviewersManifest, ImmutableContentRef ItemsManifest,
    ImmutableContentRef ItemIntentManifest, ActorReference LaunchedBy, DateTimeOffset LaunchedAt,
    Uuid? ProgramId, Uuid? RemediationOwnerMemberId, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
