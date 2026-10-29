using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

static class AuditorRequestRead
{
    public static async ValueTask<Result<T>> ClassifyAsync<T>(Func<ValueTask<Result<T>>> read)
    {
        try
        { return await read().ConfigureAwait(false); }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<T>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact auditor-request source cannot be verified: " + exception.Message,
                exception is ImmutableContentUnavailableException));
        }
    }

    public static RequestError Missing() => new(RequestErrorKind.NotFound, "The auditor request was not found.");
    public static RequestError InvalidIdentity() => new(RequestErrorKind.Validation,
        "Tenant, program and the selected record identities are required.");
}
