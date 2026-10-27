using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.decisions.list", 1)]
public sealed record ListBoundaryDecisions(Uuid TenantId, Uuid BoundaryId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<BoundaryDecisionView>>, IBoundaryProgramReadRequest, ICallable;
