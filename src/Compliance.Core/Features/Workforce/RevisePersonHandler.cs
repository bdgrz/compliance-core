using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RevisePersonHandler(PersonOperations operations) : IRequestHandler<RevisePerson, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<RevisePerson> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
