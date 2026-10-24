using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Maps only expected immutable-source failures at the bounded Policy consumer result boundary.</summary>
static class PolicySourceFailures
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<T>.Failure(PolicyReadFailure.Of(error));
        }
    }

    public static async ValueTask<Result> ClassifyAsync(Func<ValueTask<Result>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(PolicyReadFailure.Of(error));
        }
    }
}
