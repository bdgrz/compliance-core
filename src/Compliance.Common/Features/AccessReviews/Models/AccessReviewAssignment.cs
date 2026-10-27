using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     Assigns one accepted population to a reviewer. A reviewer other than the application's
///     access owner is a delegate and requires a delegation reason.
/// </summary>
public sealed record AccessReviewAssignment(Uuid PopulationId, Uuid ReviewerMemberId,
    string? DelegationReason = null);
