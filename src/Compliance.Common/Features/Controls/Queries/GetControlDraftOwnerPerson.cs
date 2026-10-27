using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.draft.owner_person.get", 1)]
public sealed record GetControlDraftOwnerPerson(Uuid TenantId, Uuid ProgramId, Uuid ControlId)
    : IRequest<ControlDraftOwnerPersonView>, IProgramScopedRequest, ICallable;
