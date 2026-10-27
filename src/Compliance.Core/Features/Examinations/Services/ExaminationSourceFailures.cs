using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Examinations.Services;

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
