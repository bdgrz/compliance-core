using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryImpactContributor :
    IImpactContributor<BoundaryView, BoundaryChange, BoundaryImpactContribution>;
