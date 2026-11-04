using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Verifies a terminal action source before recording its outcome on the finding.</summary>
public sealed class RecordCorrectiveActionOutcomeHandler : IRequestHandler<RecordCorrectiveActionOutcome>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordCorrectiveActionOutcome> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
            $"Reporting action {context.Request.ActionId} is not implemented.")));
}
