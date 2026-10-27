using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class CreateProgramHandler(ProgramOperations operations) : IRequestHandler<CreateProgram, ProgramRegistration>
{
    public ValueTask<Result<ProgramRegistration>> HandleAsync(IRequestContext<CreateProgram> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
