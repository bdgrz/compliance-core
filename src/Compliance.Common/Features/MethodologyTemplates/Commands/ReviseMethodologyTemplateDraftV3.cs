using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Authors a separate draft revision; HTTP and MCP authoring are permitted.</summary>
[Discriminator("bdgrz.methodology-templates.revise-methodology-template-draft", 3)]
public sealed record ReviseMethodologyTemplateDraftV3(Uuid CommandId, Uuid TemplateId, Uuid DraftId,
    long ExpectedRevision, IReadOnlyList<MethodologyTemplateRecordContent> Entries) : IRequest, IMethodologyTemplateDraftCommand, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
    string IMethodologyTemplateDraftCommand.Permission => IMethodologyTemplateDraftCommand.AuthorPermission;
    bool IMethodologyTemplateDraftCommand.RequiresHttp => false;
}
