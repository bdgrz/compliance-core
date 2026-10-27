using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Reinstates a suspended member episode. The route always appends a guard fact first: a restoration when the
///     episode contributes administrator availability, otherwise a neutral admission. Source restoration waits
///     behind any unfinished suspension of the same episode, and a deprovisioned episode needs a new registration.
/// </summary>
public sealed class ReinstateMemberHandler(IAggregateReader reader, AdministratorTransitionAdmission admissions,
    TimeProvider clock) : IRequestHandler<ReinstateMember, AdministratorTransitionResponse>
{
    public async ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(
        IRequestContext<ReinstateMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (AdministratorAdmissions.MemberActor(context.Actor, request.TenantId) is not { } actor)
            return Fail(RequestErrorKind.Unauthorized, "Member reinstatement requires a Bdgrz user identity.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.UserId == Uuid.Empty ||
            request.MemberEpisodeId == Uuid.Empty || request.ExpectedMemberRevision < 0)
            return Fail(RequestErrorKind.Validation,
                "A command id, tenant, user, member episode and expected member revision are required.");

        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct).ConfigureAwait(false);
        var admission = new AdministratorAdmission(request.CommandId, request.TenantId,
            AdministratorCommandKinds.Reinstate, actor, AuthoredIntentDigest.Compute(request), clock.GetUtcNow(),
            member.Id, request.MemberEpisodeId, member.Stream, null, request.ExpectedMemberRevision, guard =>
            {
                var unfinished = AdministratorAdmissions.Unfinished(guard, request.MemberEpisodeId, true,
                    static pending => pending is { IsWithdrawal: true, Reason: TenantManagerGuard.Suspended });
                if (!unfinished.IsSuccess)
                    return Result<AdministratorSourceBinding>.Failure(unfinished.Error!);
                if (guard.DeprovisionAdmitted(request.MemberEpisodeId))
                    return Result<AdministratorSourceBinding>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A deprovisioned member needs a new registration."));
                var prepared = member.PrepareReinstate(request.MemberEpisodeId, request.ExpectedMemberRevision,
                    guard.SuspensionAdmitted(request.MemberEpisodeId));
                return prepared.IsSuccess
                    ? Result<AdministratorSourceBinding>.Success(new AdministratorSourceBinding(member.MemberRevision,
                        null, null, null))
                    : Result<AdministratorSourceBinding>.Failure(prepared.Error!);
            });
        return await admissions.AdmitAsync(context, admission, ct).ConfigureAwait(false);
    }

    static Result<AdministratorTransitionResponse> Fail(RequestErrorKind kind, string message) =>
        Result<AdministratorTransitionResponse>.Failure(new RequestError(kind, message));
}
