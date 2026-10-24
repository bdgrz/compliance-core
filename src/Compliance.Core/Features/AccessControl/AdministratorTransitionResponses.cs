using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Freezes the accepted response of a guard-first administrator transition.</summary>
static class AdministratorTransitionResponses
{
    /// <summary>The HTTP status an accepted transition answers with while the process continues.</summary>
    public const int AcceptedStatus = 202;

    /// <summary>Writes the accepted response content and describes it as the HTTP 202 first response.</summary>
    public static async Task<FirstResponseDescriptor> FreezeAcceptedAsync(IImmutableContentStore content,
        string realm, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var response = new AdministratorTransitionResponse(operationId, AdministratorTransitionResponseState.Accepted,
            null, acceptedAt);
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), AdministratorTransitionResponse.Schema,
            response, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(AcceptedStatus, ImmutableContentStoreExtensions.JsonMediaType,
            AdministratorTransitionResponse.Schema, new StandardResponseBody(body));
    }
}
