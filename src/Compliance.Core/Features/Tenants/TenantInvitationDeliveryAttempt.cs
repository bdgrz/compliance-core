using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>One provider send attempt of an invitation and the result the provider returned for it.</summary>
public sealed record TenantInvitationDeliveryAttempt(Uuid AttemptId, DateTimeOffset StartedAt, string Result,
    string? FailureCode, DateTimeOffset? ResultAt)
{
    /// <summary>The send began and no result is stored; a resume sends the same attempt again.</summary>
    public const string InFlight = "in_flight";

    /// <summary>The provider accepted the message.</summary>
    public const string Accepted = "accepted";

    /// <summary>The send definitely failed.</summary>
    public const string Rejected = "rejected";
}
