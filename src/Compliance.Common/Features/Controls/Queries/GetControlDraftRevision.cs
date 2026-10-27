using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.draft.revision.get", 1)]
public sealed record GetControlDraftRevision(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision) : IRequest<ControlDraftRevisionView>, IProgramScopedRequest, ICallable;
