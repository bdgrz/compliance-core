using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class RecordTemplateSuccessorDispositionHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordTemplateSuccessorDisposition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordTemplateSuccessorDisposition> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TemplateId == Uuid.Empty || request.NewTemplateVersion <= 0)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, program, template, and successor version are required.")));

        var actor = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId,
                TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey)),
            application =>
            {
                if (application.Current is not { } current)
                    return CommandFailureRequestAdapter.ToOutcome(CommandFailure.MissingRecord(
                        "The template application was not found."));
                var latestProposal = current.SuccessorProposals.Count == 0
                    ? null
                    : current.SuccessorProposals[^1];
                if (latestProposal?.Version.VersionNumber != request.NewTemplateVersion)
                    return CommandFailureRequestAdapter.ToOutcome(CommandFailure.StateConflict(
                        "The disposition must target the current proposed successor."));
                return CommandFailureRequestAdapter.ToOutcome(application.RecordSuccessorDisposition(
                    request.ExpectedRevision, request.Disposition, request.Rationale, actor.MemberId,
                    actor.Display, clock.GetUtcNow()));
            }, context, ct);
    }
}
