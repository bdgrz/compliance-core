using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class CreateProgramV3Handler(ProgramNamedOperations operations)
    : IRequestHandler<CreateProgramV3, ProgramRegistration>
{
    public ValueTask<Result<ProgramRegistration>> HandleAsync(IRequestContext<CreateProgramV3> context,
        CancellationToken ct) => operations.CreateAsync(context, ct);
}
