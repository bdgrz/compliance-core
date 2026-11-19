using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class ChangeProgramStageHandler(ProgramNamedOperations operations) : IRequestHandler<ChangeProgramStage>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ChangeProgramStage> context, CancellationToken ct) =>
        operations.ChangeStageAsync(context, ct);
}
