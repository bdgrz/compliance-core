using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One target of the completion process: the item's stream resource, which orders the targets canonically, and
///     the item id the receivers open it by. The process factory is given only this one string per step, so it carries
///     everything a receiver command needs beyond the root source fact.
/// </summary>
readonly record struct AccessReviewCompletionTarget(string Resource, Uuid ItemId)
{
    const char Separator = '|';

    public static AccessReviewCompletionTarget Of(Uuid tenantId, Uuid campaignId, Uuid itemId) =>
        new(AccessReviewEffects.ItemStreamId(tenantId, campaignId, itemId).ToString(), itemId);

    public static AccessReviewCompletionTarget Parse(string value)
    {
        var parts = value.Split(Separator);
        if (parts.Length != 2)
            throw new FormatException("A completion target has a stream resource and an item id.");
        return new AccessReviewCompletionTarget(parts[0], Uuid.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public override string ToString() => $"{Resource}{Separator}{ItemId}";
}
