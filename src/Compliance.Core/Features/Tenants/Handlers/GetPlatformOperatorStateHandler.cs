using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class GetPlatformOperatorStateHandler(IAggregateReader reader)
    : IRequestHandler<GetPlatformOperatorState, PlatformOperatorStateView>
{
    public async ValueTask<Result<PlatformOperatorStateView>> HandleAsync(
        IRequestContext<GetPlatformOperatorState> context, CancellationToken ct)
    {
        var userId = context.Request.UserId;
        if (userId == Uuid.Empty)
            return Result<PlatformOperatorStateView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A platform user identity is required."));
        var user = await reader.HydrateAsync(new PlatformOperator(userId), ct).ConfigureAwait(false);
        var primary = await reader.HydrateAsync(new PrimaryPlatformOperators(), ct).ConfigureAwait(false);
        primary.TryGetSeat(userId, out var generation, out var status);
        return Result<PlatformOperatorStateView>.Success(new PlatformOperatorStateView(userId, user.IsActive,
            user.GrantEpisodeId, user.Version, user.PrimaryGeneration, primary.Version,
            primary.IsBootstrapComplete, primary.OccupiedSeats, primary.ConfirmedSeats, generation, status));
    }
}
