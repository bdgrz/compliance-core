using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ChangePersonDetailsHandler(PersonOperations operations) : IRequestHandler<ChangePersonDetails, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<ChangePersonDetails> context, CancellationToken ct) =>
        operations.ChangeDetailsAsync(context, ct);
}
