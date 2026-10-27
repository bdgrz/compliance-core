using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class RecordPersonHandler(PersonOperations operations) : IRequestHandler<RecordPerson, PersonRegistration>
{
    public ValueTask<Result<PersonRegistration>> HandleAsync(IRequestContext<RecordPerson> context, CancellationToken ct) =>
        operations.RecordAsync(context, ct);
}
