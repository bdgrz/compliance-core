using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Revises the current methodology-template draft with an explicit content revision.</summary>
[Discriminator("bdgrz.methodology-templates.revise-methodology-template-draft", 2)]
public sealed record ReviseMethodologyTemplateDraft(Uuid CommandId, Uuid TemplateId, long ExpectedRevision,
    MethodologyTemplateContent Content, string ChangeNotes) : IRequest, IPlatformOperatorRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
