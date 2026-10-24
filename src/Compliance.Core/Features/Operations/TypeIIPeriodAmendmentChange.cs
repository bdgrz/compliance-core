using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The complete, attributed amendment frozen at admission. The apply step derives the new period state from it
///     and the period as it is then; nothing is re-read from the request.
/// </summary>
public sealed record TypeIIPeriodAmendmentChange(DateOnly PeriodStart, DateOnly PeriodEnd, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset AmendedAt)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "type-ii-period-amendment-change.v1";
}
