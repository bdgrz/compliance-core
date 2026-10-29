using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Approves the independently reviewed Type II plan under current CEO/CISO authority.</summary>
public sealed class ApproveTypeIIOperatingPlanHandler(IAggregateExecutor executor,
    ManagementExecutiveAuthority executiveAuthority, TimeProvider clock)
    : IRequestHandler<ApproveTypeIIOperatingPlan>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveTypeIIOperatingPlan> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II plan approval requires a personal HTTP actor."));
        if (request.Version is <= 0 or > int.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A Type II plan approval must identify a positive plan version."));

        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId,
            request.ProgramId, userId, memberId, ct).ConfigureAwait(false);
        if (role is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Type II plan approval requires one current program-scoped CEO or CISO permission."));

        return await executor.ExecuteAsync(new TypeIIPlan(request.TenantId, request.ProgramId,
                request.PlanId), plan => CommandFailureRequestAdapter.ToOutcome(plan.Approve(
                plan.Revision, (int)request.Version, request.Rationale, memberId, role, display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
