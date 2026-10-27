using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
namespace Bdgrz.Compliance.Features.Commitments.Services;

/// <summary>Classifies expected native content-read failures at the consumer result boundary.</summary>
static class CommitmentSourceFailures
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<T>.Failure(CommitmentReadFailure.Of(error));
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
            return Result.Failure(CommitmentReadFailure.Of(error));
        }
    }
}
