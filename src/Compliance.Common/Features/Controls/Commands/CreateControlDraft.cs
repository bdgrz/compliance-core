using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Commands;

[Discriminator("bdgrz.control.draft.create", 1)]
public sealed record CreateControlDraft(Uuid TenantId, Uuid ProgramId, string Identifier,
    ControlDraftContent Content) : IRequest<ControlRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ControlIds.For(TenantId, ProgramId, Identifier);
}
