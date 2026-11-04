using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Discards an unapproved separate draft with a reason; authoring transports are permitted.</summary>
[Discriminator("bdgrz.methodology-templates.discard-methodology-template-draft", 1)]
public sealed record DiscardMethodologyTemplateDraft(Uuid CommandId, Uuid TemplateId, Uuid DraftId,
    string Reason) : IRequest, IMethodologyTemplateDraftCommand, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
    string IMethodologyTemplateDraftCommand.Permission => IMethodologyTemplateDraftCommand.AuthorPermission;
    bool IMethodologyTemplateDraftCommand.RequiresHttp => false;
}
