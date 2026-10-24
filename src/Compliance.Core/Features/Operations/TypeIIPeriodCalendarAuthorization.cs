using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The exact calendar reservation a period holds: the operation that was authorized and the calendar's own
///     reservation generation. A release never names anything else.
/// </summary>
public sealed record TypeIIPeriodCalendarAuthorization(Uuid AuthorizationId, long Generation);
