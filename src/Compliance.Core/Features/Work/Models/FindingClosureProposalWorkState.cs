using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Exact current proposal identity and every author excluded from ordinary independent review.</summary>
public sealed record FindingClosureProposalWorkState(Uuid ClosureId, long Revision,
    IReadOnlyList<Uuid> Authors, DateTimeOffset ProposedAt);
