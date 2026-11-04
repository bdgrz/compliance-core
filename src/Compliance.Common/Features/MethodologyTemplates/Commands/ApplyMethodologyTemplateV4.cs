using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Applies a later published version through the program's one canonical template ordering owner.</summary>
[Discriminator("bdgrz.methodology-templates.apply-methodology-template", 4)]
public sealed record ApplyMethodologyTemplateV4(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid TemplateId, long TemplateVersion) : IRequest, IIdentifiedRequest, IProgramScopedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
