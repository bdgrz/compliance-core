using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.version.get", 1)]
public sealed record GetBoundaryVersion(Uuid TenantId, Uuid BoundaryId, Uuid VersionId,
    long? MinimumBoundaryRevision = null)
    : IRequest<BoundaryVersionView>, IBoundaryProgramReadRequest, ICallable;
