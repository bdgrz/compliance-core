using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

static class AdvisorFailures
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> action, CancellationToken ct)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<T>.Failure(Error(exception));
        }
    }

    public static async ValueTask<Result> ClassifyAsync(Func<ValueTask<Result>> action, CancellationToken ct)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result.Failure(Error(exception));
        }
    }

    static RequestError Error(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The exact advisor native content cannot be verified: " + exception.Message,
        isTransient: exception is ImmutableContentUnavailableException);
}
