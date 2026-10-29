using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class RevisePersonHandler(PersonOperations operations) : IRequestHandler<RevisePerson, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<RevisePerson> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
