using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.get", 1)]
public sealed record GetBoundary(Uuid TenantId, Uuid BoundaryId, long? MinimumRevision = null)
    : IRequest<BoundaryView>, IBoundaryProgramReadRequest, ICallable;
