using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

/// <summary>Records an exact-version CEO/CISO decision using current member and RBAC authority.</summary>
public sealed class ApproveManagementAssertionHandler(IAggregateExecutor executor,
    IAggregateReader reader, ManagementExecutiveAuthority executiveAuthority, TimeProvider clock)
    : IRequestHandler<ApproveManagementAssertion>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveManagementAssertion> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Management assertion approval requires a personal HTTP actor."));
        if (request.Version is <= 0 or > int.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Select a valid management assertion version."));

        var assertion = await reader.HydrateAsync(new ManagementAssertion(request.TenantId,
            request.ProgramId, request.AssertionId), ct).ConfigureAwait(false);
        if (assertion.Current?.CurrentVersion is not { } currentVersion)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The management assertion was not found."));

        var role = await executiveAuthority.ResolveSignerRoleAsync(request.TenantId,
            request.ProgramId, userId, memberId, currentVersion.Content.Signers, ct)
            .ConfigureAwait(false);
        if (role is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Approval requires one current program grant matching the actor's named CEO or CISO signer role."));

        var expectedRevision = assertion.Revision;
        return await executor.ExecuteAsync(new ManagementAssertion(request.TenantId,
                request.ProgramId, request.AssertionId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Approve(expectedRevision,
                    (int)request.Version, request.Outcome, request.Rationale, memberId, role,
                    display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
