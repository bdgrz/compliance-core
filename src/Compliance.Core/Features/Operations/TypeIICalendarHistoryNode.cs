using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One settled reservation linked to the previous verified immutable terminal cut.</summary>
public sealed record TypeIICalendarHistoryNode(Uuid TenantId, Uuid ProgramId, long Revision, long TerminalCount,
    ImmutableContentRef? Previous, IReadOnlyList<TypeIICalendarReservationContent> Closed);
