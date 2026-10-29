using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.History;

/// <summary>One settled reservation linked to the previous verified immutable terminal cut.</summary>
public sealed record TypeIICalendarHistoryNode(Uuid TenantId, Uuid ProgramId, long Revision, long TerminalCount,
    ImmutableContentRef? Previous, IReadOnlyList<TypeIICalendarReservationContent> Closed);
