using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Expected exact-source failures at result boundaries; cancellation remains untouched.</summary>
static class WorkDigestSourceFailures
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> operation)
    {
        try
        { return await operation().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<T>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact digest source cannot be verified: " + exception.Message,
                exception is ImmutableContentUnavailableException));
        }
    }
    public static async ValueTask<Result> ClassifyAsync(Func<ValueTask<Result>> operation)
    {
        try
        { return await operation().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact digest source cannot be verified: " + exception.Message,
                exception is ImmutableContentUnavailableException));
        }
    }
}
