using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>The content rules of evidence request commands and the schemas of their frozen responses.</summary>
static class EvidenceRequestRules
{
    public const int MaximumTitleLength = 200;
    public const int MaximumTextLength = 4000;

    /// <summary>Schema of the frozen request view.</summary>
    public const string ViewSchema = "EvidenceRequest.EvidenceRequestView.v1";

    /// <summary>The normalized opening terms, or the reason the content is invalid.</summary>
    public static CommandFailure? ValidateOpen(string? title, string? instructions, Uuid ownerMemberId, Uuid? controlId,
        DateOnly dueOn, DateTimeOffset openedAt)
    {
        var cleanTitle = title?.Trim() ?? "";
        var cleanInstructions = instructions?.Trim() ?? "";
        if (cleanTitle.Length is 0 or > MaximumTitleLength)
            return CommandFailure.InvalidContent("An evidence request requires a title of at most 200 characters.");
        if (cleanInstructions.Length is 0 or > MaximumTextLength)
            return CommandFailure.InvalidContent("An evidence request requires instructions of at most 4000 characters.");
        if (ownerMemberId == Uuid.Empty || controlId == Uuid.Empty)
            return CommandFailure.InvalidContent("An evidence request requires an owner.");
        return dueOn < DateOnly.FromDateTime(openedAt.UtcDateTime)
            ? CommandFailure.InvalidContent("An evidence request cannot be due in the past.")
            : null;
    }

    /// <summary>The failure of a cancellation rationale, or null.</summary>
    public static CommandFailure? ValidateCancel(string? rationale) =>
        string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > MaximumTextLength
            ? CommandFailure.InvalidContent("Cancelling an evidence request requires a rationale.")
            : null;
}
