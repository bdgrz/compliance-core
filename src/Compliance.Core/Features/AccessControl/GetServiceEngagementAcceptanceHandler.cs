using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the current accepted snapshot of an engagement from its own owner.</summary>
public sealed class GetServiceEngagementAcceptanceHandler(IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<GetServiceEngagementAcceptance, ServiceEngagementAcceptanceView>
{
    public async ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<GetServiceEngagementAcceptance> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty)
            return NotFound();
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(request.TenantId, request.EngagementId),
            ct).ConfigureAwait(false);
        return owner.AcceptanceResult is { } result
            ? Result<ServiceEngagementAcceptanceView>.Success(await IndependenceContent
                .ReadResultAsync<ServiceEngagementAcceptanceView>(content, IndependenceContent.Realm(request.TenantId),
                    result, ct).ConfigureAwait(false))
            : NotFound();
    }

    static Result<ServiceEngagementAcceptanceView> NotFound() => Result<ServiceEngagementAcceptanceView>.Failure(
        new RequestError(RequestErrorKind.NotFound, "The accepted engagement was not found."));
}
