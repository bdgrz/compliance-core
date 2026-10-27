using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class CreateMethodologyTemplateHandler(MethodologyTemplateOperations operations) : IRequestHandler<CreateMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CreateMethodologyTemplate> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TemplateId, request.TemplateId, nameof(CreateMethodologyTemplate),
            null, (owner, member) => owner.PrepareCreate(),
            (rules, member, display, at) => rules.Create(request.Content, request.ChangeNotes, member, display, at), ct);
    }
}
