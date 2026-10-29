using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class CreateProgramHandler(ProgramOperations operations) : IRequestHandler<CreateProgram, ProgramRegistration>
{
    public ValueTask<Result<ProgramRegistration>> HandleAsync(IRequestContext<CreateProgram> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
