using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>A created review-work identity linked to one assessed change version.</summary>
public sealed record SignificantChangeWorkLinkView(
    int ContentVersion,
    Uuid WorkId,
    Uuid LinkedByMemberId,
    string LinkedByDisplay,
    DateTimeOffset LinkedAt);
