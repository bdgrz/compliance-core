using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.version.get", 1)]
public sealed record GetBoundaryVersion(Uuid TenantId, Uuid BoundaryId, Uuid VersionId,
    long? MinimumBoundaryRevision = null)
    : IRequest<BoundaryVersionView>, IBoundaryProgramReadRequest, ICallable;
