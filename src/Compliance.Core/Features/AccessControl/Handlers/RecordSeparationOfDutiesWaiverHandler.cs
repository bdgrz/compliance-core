using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class RecordSeparationOfDutiesWaiverHandler(IAggregateExecutor executor,
    ITenantMembershipDirectoryReader memberships, TimeProvider clock)
    : IRequestHandler<RecordSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>
{
    public async ValueTask<Result<SeparationOfDutiesWaiverView>> HandleAsync(
        IRequestContext<RecordSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        var request = context.Request;
        var beneficiary = await memberships.GetAsync(request.TenantId.ToString(),
            request.BeneficiaryUserId, ct).ConfigureAwait(false);
        if (beneficiary is null || beneficiary.IsSuspended || beneficiary.IsDeprovisioned ||
            beneficiary.Affiliation == "firm_staff")
            return Result<SeparationOfDutiesWaiverView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The waiver beneficiary must be an active tenant member."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var requesterUserId))
            return Result<SeparationOfDutiesWaiverView>.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "Waiver administration requires a Bdgrz user identity."));
        var requesterMemberId = RbacIds.Member(request.TenantId, requesterUserId);
        var now = clock.GetUtcNow();
        return await executor.ExecuteAsync(new SeparationOfDutiesWaiver(request.TenantId,
                context.RequestId), waiver =>
            {
                var failure = waiver.Record(request.Scope,
                    RbacIds.Member(request.TenantId, request.BeneficiaryUserId), requesterMemberId,
                    UserIdentityClaims.BdgrzDisplay(context.Actor, requesterUserId),
                    request.Rationale, now, request.ExpiresAt);
                return failure is null
                    ? AggregateOutcome.Commit(Result<SeparationOfDutiesWaiverView>.Success(
                        waiver.ToView(now)))
                    : AggregateOutcome.Discard(Result<SeparationOfDutiesWaiverView>.Failure(
                        new RequestError(failure.Code switch
                        {
                            CommandFailureCode.InvalidContent => RequestErrorKind.Validation,
                            CommandFailureCode.StateConflict => RequestErrorKind.Conflict,
                            _ => RequestErrorKind.Forbidden,
                        }, failure.Message!)));
            }, context, ct).ConfigureAwait(false);
    }
}
