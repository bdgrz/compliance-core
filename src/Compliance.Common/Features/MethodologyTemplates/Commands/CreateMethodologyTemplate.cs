using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Creates the first authored draft of a platform methodology template.</summary>
[Discriminator("bdgrz.methodology-templates.create-methodology-template", 2)]
public sealed record CreateMethodologyTemplate(Uuid TemplateId, MethodologyTemplateContent Content,
    string ChangeNotes) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => TemplateId;
}
