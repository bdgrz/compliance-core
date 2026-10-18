using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class RetireMethodologyTemplateHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RetireMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireMethodologyTemplate> context, CancellationToken ct)
    {
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var request = context.Request;
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId), template =>
                CommandFailureRequestAdapter.ToOutcome(template.Retire(request.ExpectedRevision,
                    request.EffectiveOn, request.Rationale, actor.MemberId, actor.Display, clock.GetUtcNow())),
            context, ct);
    }
}
