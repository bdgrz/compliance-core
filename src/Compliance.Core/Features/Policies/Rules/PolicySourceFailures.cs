using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Policies.Aggregates;
namespace Bdgrz.Compliance.Features.Policies.Rules;

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
