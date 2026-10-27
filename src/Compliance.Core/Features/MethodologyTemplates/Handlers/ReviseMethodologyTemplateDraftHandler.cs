using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ReviseMethodologyTemplateDraftHandler(MethodologyTemplateOperations operations) : IRequestHandler<ReviseMethodologyTemplateDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseMethodologyTemplateDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TemplateId, request.CommandId, nameof(ReviseMethodologyTemplateDraft),
            request.ExpectedRevision, (owner, member) => owner.PrepareRevise(request.ExpectedRevision),
            (rules, member, display, at) => rules.ReviseDraft(request.ExpectedRevision, request.Content, request.ChangeNotes, member, display, at), ct);
    }
}
