using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed class UserIdentityContinuation(IAggregateExecutor executor, TimeProvider? clock = null)
{
    public async ValueTask<Result<AuthenticatedUserIdentity>> ContinueAsync(
        string provider,
        string identifier,
        string? emailAddress,
        Uuid? existingUserId,
        IExecutionContext context,
        CancellationToken ct,
        bool observeProfile = false,
        string? displayName = null)
    {
        // Freeze a new account candidate once for this continuation, before aggregate preparation.
        var registrationUserId = existingUserId ?? Uuid.CreateVersion4();
        var authenticated = await executor.ExecuteAsync(
            new UserIdentity(provider, identifier),
            identity => AggregateOutcome.CommitOnSuccess(identity.IsRegistered
                ? identity.Authenticate()
                : identity.Register(registrationUserId, emailAddress)),
            context, ct).ConfigureAwait(false);
        if (!authenticated.IsSuccess || !observeProfile)
            return authenticated;

        // Registration and audits use separate append sessions. Rehydrate current authority for
        // presentation admission; audit persistence never advances its physical OCC position.
        return await executor.ExecuteAsync(new UserIdentity(provider, identifier),
            identity => AggregateOutcome.CommitOnSuccess(identity.ObserveAuthenticatedProfile(
                displayName, (clock ?? TimeProvider.System).GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
