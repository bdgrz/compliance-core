using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Changes.Models;

/// <summary>A created review-work identity linked to one assessed change version.</summary>
public sealed record SignificantChangeWorkLinkView(
    int ContentVersion,
    Uuid WorkId,
    Uuid LinkedByMemberId,
    string LinkedByDisplay,
    DateTimeOffset LinkedAt);
