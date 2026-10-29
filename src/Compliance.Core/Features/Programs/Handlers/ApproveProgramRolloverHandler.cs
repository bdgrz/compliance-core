using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Providers.Handlers;
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
