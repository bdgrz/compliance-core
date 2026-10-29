using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     Platform-side verification from a later accepted population. <c>Outcome</c> is
///     <c>removed</c> or <c>changed</c>.
/// </summary>
public sealed record AccessRemediationVerificationView(Uuid PopulationId, Uuid SnapshotId,
    Uuid CalculationId, DateTimeOffset ObservedAt, string Outcome, ActorReference VerifiedBy,
    DateTimeOffset VerifiedAt);
