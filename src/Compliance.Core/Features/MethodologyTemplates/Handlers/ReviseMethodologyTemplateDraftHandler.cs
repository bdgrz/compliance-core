using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Cntryl.Portia;

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
