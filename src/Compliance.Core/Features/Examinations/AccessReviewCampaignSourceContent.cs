using Bdgrz.Compliance.Features.AccessReviews.Models;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record AccessReviewCampaignSourceContent(DateOnly AsOf,
    IReadOnlyList<AccessReviewCampaignSourceEvent> Events);

sealed record AccessReviewCampaignSourceEvent(Uuid EventId, ulong AggregateVersion,
    DateTimeOffset RecordedAt, string Kind, JsonElement Content);

/// <summary>The completion fact of a campaign with its final snapshot reference resolved from verified content.</summary>
sealed record AccessReviewCampaignCompletionSource(long Revision, AccessReviewCampaignCompletionView Completion);

/// <summary>The decisions an item fact recorded, resolved from verified content.</summary>
sealed record AccessReviewItemDecisionsSource(Uuid ItemId, long Revision, IReadOnlyList<AccessDecisionView> Decisions);

/// <summary>The provider change an item fact recorded, resolved from verified content.</summary>
sealed record AccessReviewItemChangeSource(Uuid ItemId, long Revision, AccessRemediationChangeView Change);

/// <summary>The verification an item fact recorded, resolved from verified content.</summary>
sealed record AccessReviewItemVerificationSource(Uuid ItemId, long Revision,
    AccessRemediationVerificationView Verification);

/// <summary>The exception an item fact recorded, resolved from verified content.</summary>
sealed record AccessReviewItemExceptionSource(Uuid ItemId, long Revision, AccessRemediationExceptionView Exception);

/// <summary>The reassignment an item fact recorded, resolved from verified content.</summary>
sealed record AccessReviewItemReassignmentSource(Uuid ItemId, long Revision,
    AccessReviewResponsibilityReassignmentView Reassignment);
