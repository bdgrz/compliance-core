using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>A review decision and comments bound to one immutable description version.</summary>
public sealed record SystemDescriptionReviewView(Uuid ReviewId, Uuid VersionId, long Version,
    string Outcome, string Comments, Uuid ReviewerMemberId, string ReviewerDisplay,
    DateTimeOffset ReviewedAt);
