using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ApproveMethodologyTemplateVersionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ApproveMethodologyTemplateVersion>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveMethodologyTemplateVersion> context,
        CancellationToken ct)
    {

        var request = context.Request;
        var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
        var operation = AuthoredCommandReplay.Create(new MethodologyTemplate(request.TemplateId, request.CommandId), request.CommandId,
            nameof(ApproveMethodologyTemplateVersion), actor.MemberId, null, request, clock.GetUtcNow(), "platform_operator");
        return executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId, request.CommandId), template =>
        {
            if (AuthoredCommandReplay.Unit(template.ReplayAcceptance, operation) is { } replay)
                return AggregateOutcome.Discard(replay);
            if (template.Current is not { } current)
                return CommandFailureRequestAdapter.ToOutcome(CommandFailure.MissingRecord(
                    "The methodology template was not found."));
            if (current.Versions[^1].VersionNumber != request.Version)
                return CommandFailureRequestAdapter.ToOutcome(CommandFailure.StateConflict(
                    "Approval must target the current methodology-template version."));
            return CommandFailureRequestAdapter.ToOutcome(template.ApproveVersion(template.Revision,
                request.ApprovalRationale, actor.MemberId, actor.Display, clock.GetUtcNow(), operation: operation));
        }, context, ct);
    }
}
