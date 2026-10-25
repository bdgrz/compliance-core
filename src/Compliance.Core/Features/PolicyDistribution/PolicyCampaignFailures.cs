using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

static class PolicyCampaignFailures
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

    static RequestError Error(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The exact campaign native content cannot be verified: " + exception.Message,
        isTransient: exception is ImmutableContentUnavailableException);
}
