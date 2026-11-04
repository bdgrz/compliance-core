using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Opens only the separate draft selected by an exact committed line-start fact.</summary>
[Discriminator("bdgrz.methodology-templates.open-methodology-template-draft", 1)]
public sealed record OpenMethodologyTemplateDraft(Uuid TemplateId, Uuid DraftId, NativeFactReference Source,
    ulong SourcePosition, Uuid CommandId) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
