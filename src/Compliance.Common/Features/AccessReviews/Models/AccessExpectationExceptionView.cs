using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>An approved, expiring exception to one expectation for one principal and optional entitlement.</summary>
public sealed record AccessExpectationExceptionView(Uuid ExceptionId, Uuid ExpectationId,
    string ProviderSubjectId, string? ProviderEntitlementId, string Rationale,
    DateTimeOffset ExpiresAt, ActorReference ApprovedBy, DateTimeOffset ApprovedAt);
