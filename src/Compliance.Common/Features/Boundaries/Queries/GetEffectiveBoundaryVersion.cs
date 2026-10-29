using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.version.effective.get", 1)]
public sealed record GetEffectiveBoundaryVersion(Uuid TenantId, Uuid BoundaryId,
    DateOnly EffectiveOn, long? MinimumBoundaryRevision = null)
    : IRequest<BoundaryVersionView>, IBoundaryProgramReadRequest, ICallable;
