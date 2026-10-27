using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Programs.Commands;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class ReviseProgramHandler(ProgramOperations operations) : IRequestHandler<ReviseProgram>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseProgram> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
