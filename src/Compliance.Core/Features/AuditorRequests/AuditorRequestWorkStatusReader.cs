using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Derives request work status from verified terms and the independent response owners.</summary>
sealed class AuditorRequestWorkStatusReader(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<Result<string>> ReadAsync(Uuid tenantId, Uuid programId, Uuid requestId, DateOnly asOf,
        CancellationToken ct = default)
    {
        var request = await reader.HydrateAsync(new AuditorRequest(tenantId, programId, requestId), ct)
            .ConfigureAwait(false);
        if (!request.IsRecorded)
            return Result<string>.Success("missing");
        var terms = await AuditorCommands.TermsAsync(content, tenantId, request, ct).ConfigureAwait(false);
        if (!terms.IsSuccess)
            return Result<string>.Failure(terms.Error!);
        if (request.Resolved is not null)
            return Result<string>.Success("resolved");
        var responses = new List<AuditorResponse>();
        foreach (var admitted in request.Responses)
            responses.Add(await reader.HydrateAsync(new AuditorResponse(tenantId, programId, requestId,
                admitted.Operation.LogicalId), ct).ConfigureAwait(false));
        if (responses.Any(static response => response.Disposition?.Outcome == "accepted"))
            return Result<string>.Success("accepted");
        if (responses.Any(static response => response.Status == "rejected"))
            return Result<string>.Success("blocked");
        if (responses.Any(static response => response.Status is "submitted" or "approved" or "delivered"))
            return Result<string>.Success("submitted");
        return Result<string>.Success(asOf > terms.Value.DueOn ? "overdue" : "open");
    }
}
