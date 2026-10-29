using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.get", 1)]
public sealed record GetBoundary(Uuid TenantId, Uuid BoundaryId, long? MinimumRevision = null)
    : IRequest<BoundaryView>, IBoundaryProgramReadRequest, ICallable;
