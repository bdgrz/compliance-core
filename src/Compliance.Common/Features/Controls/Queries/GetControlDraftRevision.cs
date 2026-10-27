using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.draft.revision.get", 1)]
public sealed record GetControlDraftRevision(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision) : IRequest<ControlDraftRevisionView>, IProgramScopedRequest, ICallable;
