using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>Executes grants and revocations on the affected user's independent owner.</summary>
public sealed class PlatformOperatorOperations(IAggregateExecutor executor, IAggregateReader reader,
    IPlatformUserDirectoryReader users, PlatformOperatorAuthority configuration)
{
    /// <summary>Grants one identified user at that user's expected revision.</summary>
    public async ValueTask<Result> GrantAsync(IRequestContext<GrantPlatformOperator> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actor) || RequestActor.IsSystem(context.Actor))
            return Forbidden();
        if (request.UserId == Uuid.Empty || request.CommandId == Uuid.Empty)
            return Invalid();
        if (!await InitializedAsync(ct).ConfigureAwait(false))
            return Pending();
        var observed = await reader.HydrateAsync(new PlatformOperator(request.UserId, request.CommandId), ct).ConfigureAwait(false);
        // A prior exact grant remains replayable even if the discovery projection later lags.
        if (observed.SelectedOutcome is null && !await users.ExistsAsync(request.UserId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The platform user is not available yet.", true));
        try
        {
            return await executor.ExecuteAsync(new PlatformOperator(request.UserId, request.CommandId), owner =>
                AggregateOutcome.CommitOnSuccess(owner.Grant(request.CommandId, request.ExpectedRevision, actor,
                    RbacOperationActor.From(context.Actor), request.Reason)), context, ct).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
    }

    /// <summary>Revokes the exact unmarked grant at that user's expected revision.</summary>
    public async ValueTask<Result> RevokeAsync(IRequestContext<RevokePlatformOperator> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actor) || RequestActor.IsSystem(context.Actor))
            return Forbidden();
        if (request.UserId == Uuid.Empty || request.CommandId == Uuid.Empty || request.AssignmentId == Uuid.Empty)
            return Invalid();
        if (!await InitializedAsync(ct).ConfigureAwait(false))
            return Pending();
        try
        {
            return await executor.ExecuteAsync(new PlatformOperator(request.UserId, request.CommandId), owner =>
                AggregateOutcome.CommitOnSuccess(owner.Revoke(request.CommandId, request.ExpectedRevision, request.AssignmentId,
                    actor, RbacOperationActor.From(context.Actor), request.Reason)), context, ct).ConfigureAwait(false);
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
    }

    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden, "An authenticated operator is required."));
    async ValueTask<bool> InitializedAsync(CancellationToken ct) =>
        configuration.DeveloperAuthentication && configuration.BootstrapUserIds.Count == 0 ||
        (await reader.HydrateAsync(new PrimaryPlatformOperators(), ct).ConfigureAwait(false)).IsBootstrapComplete;
    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation, "An exact user and operation identity are required."));
    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The operator changed; retry the exact operation.", true));
}
