using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Expected missing/corrupt immutable source failures at a result boundary; cancellation is unchanged.</summary>
static class ExaminationSourceFailures
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> read)
    {
        try
        { return await read().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<T>.Failure(Failure(exception));
        }
    }
    public static async ValueTask<Result> ClassifyAsync(Func<ValueTask<Result>> read)
    {
        try
        { return await read().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(Failure(exception));
        }
    }
    static RequestError Failure(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The exact immutable examination source cannot be verified: " + exception.Message,
        exception is ImmutableContentUnavailableException);
}
