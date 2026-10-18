using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class CreateMethodologyTemplateHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CreateMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CreateMethodologyTemplate> context, CancellationToken ct)
    {
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var request = context.Request;
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId), template =>
                CommandFailureRequestAdapter.ToOutcome(template.Create(request.Content, request.ChangeNotes,
                    actor.MemberId, actor.Display, clock.GetUtcNow())), context, ct);
    }
}
