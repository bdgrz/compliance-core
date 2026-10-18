using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ApproveMethodologyTemplateVersionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ApproveMethodologyTemplateVersion>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveMethodologyTemplateVersion> context,
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
                    "Approval must target the current methodology-template version."));
            return CommandFailureRequestAdapter.ToOutcome(template.ApproveVersion(template.Revision,
                request.ApprovalRationale, actor.MemberId, actor.Display, clock.GetUtcNow()));
        }, context, ct);
    }
}
