using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ReviewMethodologyTemplateHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviewMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewMethodologyTemplate> context,
        CancellationToken ct)
    {
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var request = context.Request;
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId), template =>
        {
            if (template.Current is not { } current)
                return CommandFailureRequestAdapter.ToOutcome(CommandFailure.MissingRecord(
                    "The methodology template was not found."));
            if (current.Versions[^1].VersionNumber != request.Version)
                return CommandFailureRequestAdapter.ToOutcome(CommandFailure.StateConflict(
                    "The review must target the current methodology-template version."));
            return CommandFailureRequestAdapter.ToOutcome(template.ReviewDraft(template.Revision,
                request.Outcome, request.Comments, actor.MemberId, actor.Display, clock.GetUtcNow()));
        }, context, ct);
    }
}
