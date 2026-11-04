using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Personally approves an independently accepted separate draft over HTTP.</summary>
[Discriminator("bdgrz.methodology-templates.approve-methodology-template-draft", 1)]
public sealed record ApproveMethodologyTemplateDraft(Uuid CommandId, Uuid TemplateId, Uuid DraftId,
    long Revision) : IRequest, IMethodologyTemplateDraftCommand, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
    string IMethodologyTemplateDraftCommand.Permission => IMethodologyTemplateDraftCommand.ApprovePermission;
    bool IMethodologyTemplateDraftCommand.RequiresHttp => true;
}
