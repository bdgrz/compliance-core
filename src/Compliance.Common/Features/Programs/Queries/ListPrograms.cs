using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.program.list", 1)]
public sealed record ListPrograms(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProgramView>>, IProgramManagementRequest, ICallable;
