using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>Classifies expected native content-read failures at the consumer result boundary.</summary>
static class ControlSourceFailures
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<T>.Failure(ControlReadFailure.Of(error));
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
            return Result.Failure(ControlReadFailure.Of(error));
        }
    }
}
