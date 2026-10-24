using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ReviseMethodologyTemplateDraftHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseMethodologyTemplateDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseMethodologyTemplateDraft> context,
        CancellationToken ct)
    {

        var request = context.Request;
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var operation = AuthoredCommandReplay.Create(new MethodologyTemplate(request.TemplateId, request.CommandId), request.CommandId,
            nameof(ReviseMethodologyTemplateDraft), actor.MemberId, request.ExpectedRevision, request, clock.GetUtcNow(), "platform_operator");
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId, request.CommandId), template =>
                CommandFailureRequestAdapter.ToOutcome(template.ReviseDraft(request.ExpectedRevision,
                    request.Content, request.ChangeNotes, actor.MemberId, actor.Display, clock.GetUtcNow(), operation: operation)),
            context, ct);
    }
}
