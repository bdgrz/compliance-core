using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Personally accepts or returns one separate draft revision over HTTP.</summary>
[Discriminator("bdgrz.methodology-templates.review-methodology-template-draft", 1)]
public sealed record ReviewMethodologyTemplateDraft(Uuid CommandId, Uuid TemplateId, Uuid DraftId,
    long Revision, bool Accept, string Comments) : IRequest, IMethodologyTemplateDraftCommand, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
    string IMethodologyTemplateDraftCommand.Permission => IMethodologyTemplateDraftCommand.AuthorPermission;
    bool IMethodologyTemplateDraftCommand.RequiresHttp => true;
}
