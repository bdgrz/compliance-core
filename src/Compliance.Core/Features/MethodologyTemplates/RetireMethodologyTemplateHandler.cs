using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class RetireMethodologyTemplateHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RetireMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireMethodologyTemplate> context, CancellationToken ct)
    {

        var request = context.Request;
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var operation = AuthoredCommandReplay.Create(new MethodologyTemplate(request.TemplateId, request.CommandId), request.CommandId,
            nameof(RetireMethodologyTemplate), actor.MemberId, request.ExpectedRevision, request, clock.GetUtcNow(), "platform_operator");
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId, request.CommandId), template =>
                CommandFailureRequestAdapter.ToOutcome(template.Retire(request.ExpectedRevision,
                    request.EffectiveOn, request.Rationale, actor.MemberId, actor.Display, clock.GetUtcNow(), operation: operation)),
            context, ct);
    }
}
