using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ReviseMethodologyTemplateDraftHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseMethodologyTemplateDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseMethodologyTemplateDraft> context,
        CancellationToken ct)
    {
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var request = context.Request;
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId), template =>
                CommandFailureRequestAdapter.ToOutcome(template.ReviseDraft(request.ExpectedRevision,
                    request.Content, request.ChangeNotes, actor.MemberId, actor.Display, clock.GetUtcNow())),
            context, ct);
    }
}
