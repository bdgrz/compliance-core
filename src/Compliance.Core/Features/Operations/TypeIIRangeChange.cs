using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>A verified accepted opening or amendment with the period owner it was found on, or why it is not verified.</summary>
sealed record TypeIIRangeChange(Uuid TenantId, TypeIIPeriod? Period, DomainEvent? Accepted, long AcceptedRevision,
    TypeIIRangeIntent? Intent, RequestError? Error)
{
    public static TypeIIRangeChange Failed(RequestError error) => new(Uuid.Empty, null, null, 0, null, error);
}
