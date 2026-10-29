using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     An approved exception for an in-scope system instance that has no accepted population.
///     It is effective from approval until <c>ExpiresAt</c>.
/// </summary>
public sealed record AccessPopulationExceptionView(Uuid ExceptionId, Uuid SystemInstanceId,
    string Reason, DateTimeOffset ExpiresAt, ActorReference ApprovedBy, DateTimeOffset ApprovedAt);
