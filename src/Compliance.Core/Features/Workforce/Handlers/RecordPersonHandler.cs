using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RecordPersonHandler(PersonOperations operations) : IRequestHandler<RecordPerson, PersonRegistration>
{
    public ValueTask<Result<PersonRegistration>> HandleAsync(IRequestContext<RecordPerson> context, CancellationToken ct) =>
        operations.RecordAsync(context, ct);
}
