using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class ReviseProgramTargetsHandler(ProgramNamedOperations operations)
    : IRequestHandler<ReviseProgramTargets>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseProgramTargets> context, CancellationToken ct) =>
        operations.ReviseTargetsAsync(context, ct);
}
