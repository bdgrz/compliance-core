namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryImpactContribution(string Context,
    IReadOnlyList<BoundaryAffectedRecord> Records, bool Complete);
