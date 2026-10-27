using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Proposes a reviewed client disposition for one published template successor.</summary>
[Discriminator("bdgrz.methodology-templates.propose-template-successor-adoption", 2)]
public sealed record ProposeTemplateSuccessorAdoption(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    string ApplicationKey, long ExpectedRevision, long NewTemplateVersion, string Disposition)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
