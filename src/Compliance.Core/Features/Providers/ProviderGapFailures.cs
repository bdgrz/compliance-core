using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

static class ProviderGapFailures
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
            return Result<T>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact provider gap native authority cannot be verified: " + exception.Message,
                exception is ImmutableContentUnavailableException));
        }
    }
}
