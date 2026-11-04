using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Starts the line's sole separate draft from its latest published entries, or empty.</summary>
[Discriminator("bdgrz.methodology-templates.start-methodology-template-draft", 1)]
public sealed record StartMethodologyTemplateDraft(Uuid CommandId, Uuid TemplateId, Uuid DraftId)
    : IRequest, IMethodologyTemplateDraftCommand, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
    string IMethodologyTemplateDraftCommand.Permission => IMethodologyTemplateDraftCommand.AuthorPermission;
    bool IMethodologyTemplateDraftCommand.RequiresHttp => false;
}
