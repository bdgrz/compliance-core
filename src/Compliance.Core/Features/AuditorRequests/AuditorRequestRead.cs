using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

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
