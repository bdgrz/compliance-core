using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

// Coverage describes the projected snapshot, not the current authoritative source head.

/// <summary>A bounded page of source-recorded disclosure facts.</summary>
public sealed record RuntimeCriteriaDisclosurePage(Uuid EditionId, string Generation,
    string Coverage, IReadOnlyList<RuntimeCriteriaDisclosure> Items, string? NextCursor);
