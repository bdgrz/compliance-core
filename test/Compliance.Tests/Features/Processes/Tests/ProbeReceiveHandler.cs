using Cntryl.Portia;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

sealed class ProbeReceiveHandler(ProbeReceiveRecorder recorder) : IRequestHandler<ProbeReceive, ReceiverOutcome>,
    IRequestAuthorizer<ProbeReceive>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ProbeReceive> context, CancellationToken ct) =>
        ValueTask.FromResult(recorder.Handle(context.Request));

    public ValueTask<Result> AuthorizeAsync(IRequestContext<ProbeReceive> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "System only.")));
}
