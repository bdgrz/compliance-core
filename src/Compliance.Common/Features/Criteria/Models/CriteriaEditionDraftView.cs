using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>The source-backed state of one operator-managed criteria edition.</summary>
public sealed record CriteriaEditionDraftView(Uuid EditionId, string Status, string? Label,
    Uuid? PendingOperationId, string? Reason = null, string? Framework = null);
