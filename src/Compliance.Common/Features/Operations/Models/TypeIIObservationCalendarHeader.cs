using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Bounded current authority of one program's range guard.</summary>
public sealed record TypeIIObservationCalendarHeader(Uuid TenantId, Uuid ProgramId, long Revision,
    long ReservationGeneration, int LiveCount, long TerminalCount);
