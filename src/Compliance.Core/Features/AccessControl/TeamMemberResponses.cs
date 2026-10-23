using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Freezes and reads the completed response of a direct team assignment or removal.</summary>
static class TeamMemberResponses
{
    /// <summary>Writes the completed response content and describes it as the HTTP 200 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeCompletedAsync(IImmutableContentStore content,
        string realm, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var response = new AdministratorTransitionResponse(operationId, AdministratorTransitionResponseState.Completed,
            null, acceptedAt);
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AdministratorTransitionResponse.Schema,
            response, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
            AdministratorTransitionResponse.Schema, new StandardResponseBody(body));
    }

    /// <summary>The response a committed or replayed acceptance froze, read back as verified content.</summary>
    public static async Task<Result<AdministratorTransitionResponse>> ReadAsync(IImmutableContentStore content,
        string realm, Result<AcceptedAppendResult> accepted, CancellationToken ct)
    {
        if (!accepted.IsSuccess)
            return Result<AdministratorTransitionResponse>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<AdministratorTransitionResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body ||
            descriptor.ResultSchema != AdministratorTransitionResponse.Schema)
            throw new ImmutableContentIntegrityException("The accepted team operation froze an unexpected response.");
        return Result<AdministratorTransitionResponse>.Success(
            await content.ReadJsonAsync<AdministratorTransitionResponse>(realm, body.Content, ct).ConfigureAwait(false));
    }
}
