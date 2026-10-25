using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class CreateProgramHandler(ProgramOperations operations) : IRequestHandler<CreateProgram, ProgramRegistration>
{
    public ValueTask<Result<ProgramRegistration>> HandleAsync(IRequestContext<CreateProgram> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
