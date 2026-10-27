using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.draft.list", 1)]
public sealed record ListControlDrafts(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null) : IRequest<Page<ControlDraftView>>, IProgramScopedRequest, ICallable;
