using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.revisions.list", 1)]
public sealed record ListProgramRevisions(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null, long? MinimumProgramRevision = null)
    : IRequest<Page<ProgramRevisionView>>, IProgramReadRequest, ICallable;
