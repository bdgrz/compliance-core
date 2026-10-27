using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryMutationContent(IReadOnlyList<IBoundaryMutationContent> Changes,
    IReadOnlyList<SeparationOfDutiesWaiverProof>? Waivers = null, IBoundaryMutationContent? Receipt = null);
