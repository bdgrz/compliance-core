using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Only current live ranges, plus an immutable terminal-history head.</summary>
public sealed record TypeIICalendarAuthority(Uuid TenantId, Uuid ProgramId, long Revision, long ReservationGeneration,
    long TerminalCount, ImmutableContentRef? ReleasedHistory, IReadOnlyList<TypeIICalendarReservationContent> Live);
