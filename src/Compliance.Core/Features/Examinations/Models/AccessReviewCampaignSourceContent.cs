using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Models;

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
