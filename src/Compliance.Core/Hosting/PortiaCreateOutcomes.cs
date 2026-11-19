using Cntryl.Portia;

namespace Bdgrz.Compliance.Hosting;

public static class PortiaCreateOutcomes
{
    public static Result<PortiaCreateOutcome<T>> From<T>(Result<T> result, bool created)
    {
        if (!result.IsSuccess)
            return Result<PortiaCreateOutcome<T>>.Failure(result.Error!);

        var outcome = created
            ? PortiaCreateOutcome<T>.Created(result.Value!)
            : PortiaCreateOutcome<T>.Repeated(result.Value!);
        return Result<PortiaCreateOutcome<T>>.Success(outcome);
    }
}
