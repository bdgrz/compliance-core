using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class RevisePersonHandler(PersonOperations operations) : IRequestHandler<RevisePerson, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<RevisePerson> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
