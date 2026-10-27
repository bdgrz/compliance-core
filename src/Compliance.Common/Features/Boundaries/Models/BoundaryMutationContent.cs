using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryMutationContent(IReadOnlyList<IBoundaryMutationContent> Changes,
    IReadOnlyList<SeparationOfDutiesWaiverProof>? Waivers = null, IBoundaryMutationContent? Receipt = null);
