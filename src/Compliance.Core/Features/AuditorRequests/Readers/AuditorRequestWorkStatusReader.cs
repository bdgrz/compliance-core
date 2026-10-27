using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Readers;

/// <summary>Derives status from verified request content and independent response owners.</summary>
sealed class AuditorRequestWorkStatusReader(AuditorRequestReader requests)
{
    public async ValueTask<Result<string>> ReadAsync(Uuid tenantId, Uuid programId, Uuid requestId, DateOnly asOf,
        CancellationToken ct = default)
    {
        try
        {
            var snapshot = await requests.ReadAsync(tenantId, programId, requestId, ct).ConfigureAwait(false);
            return Result<string>.Success(snapshot.View is not { } view ? "missing" :
                AuditorRequestReader.WorkStatus(snapshot.Owner, view.Responses, view.DueOn, asOf));
        }
        catch (Content.ImmutableContentIntegrityException error)
        {
            return Result<string>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The request work status cannot be verified: {error.Message}", error is Content.ImmutableContentUnavailableException));
        }
    }
}
