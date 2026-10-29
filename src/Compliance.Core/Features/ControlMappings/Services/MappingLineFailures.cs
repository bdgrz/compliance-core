using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.ControlMappings.Services;

static class MappingLineFailures
{
    public static async ValueTask<Result<T>> ExecuteAsync<T>(Func<ValueTask<Result<T>>> action, CancellationToken ct)
    {
        try
        { return await action().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<T>.Failure(Error(exception));
        }
    }
    public static async ValueTask<Result> ExecuteAsync(Func<ValueTask<Result>> action, CancellationToken ct)
    {
        try
        { return await action().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result.Failure(Error(exception));
        }
    }
    static RequestError Error(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The exact control mapping content cannot be verified: " + exception.Message,
        isTransient: exception is ImmutableContentUnavailableException);
}
