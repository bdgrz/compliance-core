using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record AccessReviewCampaignSourceContent(DateOnly AsOf,
    IReadOnlyList<AccessReviewCampaignSourceEvent> Events);

sealed record AccessReviewCampaignSourceEvent(Uuid EventId, ulong AggregateVersion,
    DateTimeOffset RecordedAt, string Kind, JsonElement Content);
