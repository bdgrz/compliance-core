using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Native close approval over an exact ready reconciliation, with current executive authorization before replay.</summary>
public sealed class ApproveTypeIIPeriodCloseHandler(TypeIIPeriodOperations operations,
    ManagementExecutiveAuthority executiveAuthority) : IRequestHandler<ApproveTypeIIPeriodClose, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<ApproveTypeIIPeriodClose> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId, out var member, out var display) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Close approval requires a personal HTTP actor."));
        var role = await executiveAuthority.ResolveExecutiveRoleAsync(request.TenantId, request.ProgramId, user, member, ct).ConfigureAwait(false);
        if (role is null)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden, "Close approval requires current program-scoped CEO or CISO authority."));
        return await operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.PeriodId, request.CommandId,
            nameof(ApproveTypeIIPeriodClose), request.ExpectedRevision, (snapshot, operation, _) => ValueTask.FromResult(
                TypeIIPeriodOperations.Outcome(snapshot.Rules.ApproveClose(request.ExpectedRevision, request.AcknowledgedMaterialItemIds,
                    request.Rationale, member, role, display, operation.AcceptedAt))), ct).ConfigureAwait(false);
    }
}
