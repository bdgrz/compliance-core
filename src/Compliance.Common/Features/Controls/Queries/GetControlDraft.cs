using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.draft.get", 1)]
public sealed record GetControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long? MinimumRevision = null) : IRequest<ControlDraftView>, IProgramScopedRequest, ICallable;
