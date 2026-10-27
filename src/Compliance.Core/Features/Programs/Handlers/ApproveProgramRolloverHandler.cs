using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

/// <summary>Records an exact-version rollover decision using current member and scoped authority.</summary>
public sealed class ApproveProgramRolloverHandler(IAggregateExecutor executor,
    IAggregateReader reader, ProgramRolloverApproverAuthority approverAuthority,
    TimeProvider clock) : IRequestHandler<ApproveProgramRollover>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveProgramRollover> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Program rollover approval requires a personal HTTP actor."));
        if (request.Version is <= 0 or > int.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Select a valid program rollover version."));

        var rollover = await reader.HydrateAsync(new ProgramRollover(request.TenantId,
            request.ProgramId, request.RolloverId), ct).ConfigureAwait(false);
        if (rollover.Current?.CurrentVersion is not { } currentVersion)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program rollover was not found."));
        if (request.Version != currentVersion.Version)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The selected program rollover version is no longer current."));

        var role = await approverAuthority.ResolveRoleAsync(request.TenantId, request.ProgramId,
            userId, memberId, ct).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Approval requires current program-management permission and one unambiguous Compliance Lead, CEO, or CISO authority."));

        return await executor.ExecuteAsync(new ProgramRollover(request.TenantId,
                request.ProgramId, request.RolloverId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Approve(rollover.Revision,
                    request.Decision, request.Rationale, memberId, role, display,
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
