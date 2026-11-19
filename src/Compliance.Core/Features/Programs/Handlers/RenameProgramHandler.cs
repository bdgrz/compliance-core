using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class RenameProgramHandler(ProgramNamedOperations operations) : IRequestHandler<RenameProgram>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RenameProgram> context, CancellationToken ct) =>
        operations.RenameAsync(context, ct);
}
