using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records the reviewed final disposition for one proposed template successor.</summary>
[Discriminator("bdgrz.methodology-templates.record-template-successor-disposition", 2)]
public sealed record RecordTemplateSuccessorDisposition(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    string ApplicationKey, long ExpectedRevision, long NewTemplateVersion, string Disposition, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
