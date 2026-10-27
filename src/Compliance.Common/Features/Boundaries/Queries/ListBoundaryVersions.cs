using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.versions.list", 1)]
public sealed record ListBoundaryVersions(Uuid TenantId, Uuid BoundaryId,
    int? Limit = null, string? Cursor = null, long? MinimumBoundaryRevision = null)
    : IRequest<Page<BoundaryVersionView>>, IBoundaryProgramReadRequest, ICallable;
