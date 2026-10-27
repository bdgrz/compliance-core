using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ReviseProgramHandler(ProgramOperations operations) : IRequestHandler<ReviseProgram>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseProgram> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
