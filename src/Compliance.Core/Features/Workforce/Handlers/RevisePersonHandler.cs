using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>Refuses generic writes while retaining the registered retired request contract.</summary>
public sealed class RevisePersonHandler : IRequestHandler<RevisePerson, UnitResult>
{
    ValueTask<Result<UnitResult>> IRequestHandler<RevisePerson, UnitResult>.HandleAsync(
        IRequestContext<RevisePerson> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Conflict,
            "Generic person revision is retired; use ChangePersonDetails.")));
}
