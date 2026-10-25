using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Only current live ranges, plus an immutable terminal-history head.</summary>
public sealed record TypeIICalendarAuthority(Uuid TenantId, Uuid ProgramId, long Revision, long ReservationGeneration,
    long TerminalCount, ImmutableContentRef? ReleasedHistory, IReadOnlyList<TypeIICalendarReservationContent> Live);
