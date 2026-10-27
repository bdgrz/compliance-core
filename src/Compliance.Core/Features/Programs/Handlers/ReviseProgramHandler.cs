using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class ReviseProgramHandler(ProgramOperations operations) : IRequestHandler<ReviseProgram>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseProgram> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
