using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class RecordTemplateSuccessorDispositionHandler(TemplateApplicationOperations operations)
    : IRequestHandler<RecordTemplateSuccessorDisposition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordTemplateSuccessorDisposition> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.NewTemplateVersion <= 0)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An exact proposed successor version is required.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.TemplateId,
            request.ApplicationKey, request.CommandId, nameof(RecordTemplateSuccessorDisposition), request.ExpectedRevision,
            (rules, actor, display, at, _) =>
            {
                var proposal = rules.Current?.SuccessorProposals is { Count: > 0 } proposals ? proposals[^1] : null;
                if (proposal?.Version.VersionNumber != request.NewTemplateVersion)
                    return ValueTask.FromResult<CommandFailure?>(CommandFailure.StateConflict(
                        "The disposition must target the current proposed successor."));
                return ValueTask.FromResult(rules.RecordSuccessorDisposition(request.ExpectedRevision,
                    request.Disposition, request.Rationale, actor, display, at));
            }, ct);
    }
}
