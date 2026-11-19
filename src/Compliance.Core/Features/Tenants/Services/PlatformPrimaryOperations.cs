using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>Starts or recovers one primary designation or release.</summary>
public sealed class PlatformPrimaryOperations(IAggregateReader reader, IAggregateExecutor executor,
    PlatformOperatorAuthority configuration, IHostEnvironment environment)
{
    /// <summary>Reserves an exact current operator grant; returns pending until the mark is confirmed.</summary>
    public async ValueTask<Result> DesignateAsync(IRequestContext<DesignatePlatformPrimary> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!Actor(context, out var user))
            return Forbidden();
        if (request.UserId == Uuid.Empty || request.CommandId == Uuid.Empty)
            return Invalid();
        try
        {
            var selected = await reader.HydrateAsync(new PrimaryPlatformOperators(request.CommandId), ct).ConfigureAwait(false);
            if (selected.SelectedCommand is { } prior)
            {
                if (prior is not PrimaryPlatformOperatorSeatReserved reservation || reservation.Bootstrap && !reservation.InitialExistingGrant ||
                    reservation.UserId != request.UserId || reservation.ExpectedRevision != request.ExpectedRevision ||
                    reservation.ActorUserId != user || reservation.Actor != RbacOperationActor.From(context.Actor))
                    return Conflict();
                return selected.SelectedOutcome switch
                {
                    PrimaryPlatformOperatorAppointed => Result.Success,
                    PrimaryPlatformOperatorSeatRefused => Conflict(),
                    _ => Pending(),
                };
            }
            var target = await reader.HydrateAsync(new PlatformOperator(request.UserId), ct).ConfigureAwait(false);
            if (!target.IsActive)
                return Conflict();
            var grant = target.GrantEpisodeId;
            if (!selected.HasConfirmedInitialPrimary && configuration.DeveloperAuthentication && environment.IsDevelopment() &&
                configuration.BootstrapUserIds.Count == 0 && request.UserId == user)
            {
                var plan = AggregateIdentity.Create("PlatformDevelopmentGenesis", user);
                var initial = await executor.ExecuteAsync(new PrimaryPlatformOperators(request.CommandId), owner =>
                    AggregateOutcome.CommitOnSuccess(owner.ReserveInitialExisting(request.CommandId, request.ExpectedRevision,
                        request.UserId, grant, plan, plan, user, RbacOperationActor.From(context.Actor))), context, ct).ConfigureAwait(false);
                return initial.IsSuccess ? Pending() : initial;
            }
            if (!selected.IsBootstrapComplete)
                return Pending();
            var result = await executor.ExecuteAsync(new PrimaryPlatformOperators(request.CommandId), owner =>
                AggregateOutcome.CommitOnSuccess(owner.ReservePrimary(request.CommandId, request.ExpectedRevision,
                    request.UserId, grant, user, RbacOperationActor.From(context.Actor))), context, ct).ConfigureAwait(false);
            return result.IsSuccess ? Pending() : result;
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
    }

    /// <summary>Releases an exact primary only with a confirmed replacement; returns pending until unmarked.</summary>
    public async ValueTask<Result> ReleaseAsync(IRequestContext<ReleasePlatformPrimary> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!Actor(context, out var user))
            return Forbidden();
        if (request.UserId == Uuid.Empty || request.CommandId == Uuid.Empty || request.Generation == 0 ||
            string.IsNullOrWhiteSpace(request.Reason))
            return Invalid();
        try
        {
            var selected = await reader.HydrateAsync(new PrimaryPlatformOperators(request.CommandId), ct).ConfigureAwait(false);
            if (selected.SelectedCommand is { } prior)
            {
                if (prior is not PrimaryPlatformOperatorSeatReleased release || release.UserId != request.UserId ||
                    release.Generation != request.Generation || release.ExpectedRevision != request.ExpectedRevision ||
                    release.ActorUserId != user || release.Actor != RbacOperationActor.From(context.Actor) ||
                    release.Reason != request.Reason.Trim())
                    return Conflict();
                return selected.SelectedOutcome is PrimaryPlatformOperatorSeatVacated ? Result.Success : Pending();
            }
            if (!selected.IsBootstrapComplete)
                return Pending();
            var result = await executor.ExecuteAsync(new PrimaryPlatformOperators(request.CommandId), owner =>
                AggregateOutcome.CommitOnSuccess(owner.Release(request.CommandId, request.ExpectedRevision, request.UserId,
                    request.Generation, user, RbacOperationActor.From(context.Actor), request.Reason)), context, ct).ConfigureAwait(false);
            return result.IsSuccess ? Pending() : result;
        }
        catch (EventStreamConcurrencyException) { return Pending(); }
    }

    static bool Actor(IRequestContext context, out Uuid user)
    {
        user = Uuid.Empty;
        return !RequestActor.IsSystem(context.Actor) && UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out user);
    }
    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden, "An authenticated operator is required."));
    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation, "The exact primary operation is required."));
    static Result Conflict() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The primary operation conflicts with current state."));
    static Result Pending() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The primary transition is pending.", true));
}
