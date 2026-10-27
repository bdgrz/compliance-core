using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Processes;

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
