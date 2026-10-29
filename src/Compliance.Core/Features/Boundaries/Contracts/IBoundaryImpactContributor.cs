using Bdgrz.Compliance.Features.Versioning.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Contracts;

public interface IBoundaryImpactContributor :
    IImpactContributor<BoundaryView, BoundaryChange, BoundaryImpactContribution>;
