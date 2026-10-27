using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.program.list", 1)]
public sealed record ListProgramBoundaries(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<BoundaryView>>, IProgramReadRequest, ICallable;
