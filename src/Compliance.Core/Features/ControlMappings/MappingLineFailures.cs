using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

static class MappingLineFailures
{
    public static async ValueTask<Result<T>> ExecuteAsync<T>(Func<ValueTask<Result<T>>> action, CancellationToken ct)
    {
        try
        { return await action().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<T>.Failure(Error(exception));
        }
    }
    public static async ValueTask<Result> ExecuteAsync(Func<ValueTask<Result>> action, CancellationToken ct)
    {
        try
        { return await action().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result.Failure(Error(exception));
        }
    }
    static RequestError Error(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The exact control mapping content cannot be verified: " + exception.Message,
        isTransient: exception is ImmutableContentUnavailableException);
}
