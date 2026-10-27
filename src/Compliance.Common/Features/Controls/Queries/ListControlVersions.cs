using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.versions.list", 1)]
public sealed record ListControlVersions(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlVersionView>>, IProgramScopedRequest, ICallable;
