using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
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
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Suspends a member episode. The route always appends a guard fact first: a withdrawal when the episode
///     contributes administrator availability or has a pending registration, otherwise a neutral admission. The
///     member is suspended afterwards by the administrator transition process.
/// </summary>
public sealed class SuspendMemberHandler(IAggregateReader reader, AdministratorTransitionAdmission admissions,
    TimeProvider clock) : IRequestHandler<SuspendMember, AdministratorTransitionResponse>
{
    public async ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(
        IRequestContext<SuspendMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (AdministratorAdmissions.MemberActor(context.Actor, request.TenantId) is not { } actor)
            return Fail(RequestErrorKind.Unauthorized, "Member suspension requires a Bdgrz user identity.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.UserId == Uuid.Empty ||
            request.MemberEpisodeId == Uuid.Empty || request.ExpectedMemberRevision < 0)
            return Fail(RequestErrorKind.Validation,
                "A command id, tenant, user, member episode and expected member revision are required.");
        if (UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId) && actorUserId == request.UserId)
            return Fail(RequestErrorKind.Conflict, "An administrator cannot suspend their own tenant membership.");

        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct).ConfigureAwait(false);
        var admission = new AdministratorAdmission(request.CommandId, request.TenantId, AdministratorCommandKinds.Suspend,
            actor, AuthoredIntentDigest.Compute(request), clock.GetUtcNow(), member.Id, request.MemberEpisodeId,
            member.Stream, null, request.ExpectedMemberRevision, guard =>
            {
                var unfinished = AdministratorAdmissions.Unfinished(guard, request.MemberEpisodeId, true, static _ => false);
                if (!unfinished.IsSuccess)
                    return Result<AdministratorSourceBinding>.Failure(unfinished.Error!);
                if (guard.DeprovisionAdmitted(request.MemberEpisodeId))
                    return Result<AdministratorSourceBinding>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The member is being deprovisioned."));
                var prepared = member.PrepareSuspend(request.MemberEpisodeId, request.ExpectedMemberRevision,
                    request.Reason, guard.SuspensionAdmitted(request.MemberEpisodeId));
                return prepared.IsSuccess
                    ? Result<AdministratorSourceBinding>.Success(new AdministratorSourceBinding(member.MemberRevision,
                        null, null, request.Reason.Trim()))
                    : Result<AdministratorSourceBinding>.Failure(prepared.Error!);
            });
        return await admissions.AdmitAsync(context, admission, ct).ConfigureAwait(false);
    }

    static Result<AdministratorTransitionResponse> Fail(RequestErrorKind kind, string message) =>
        Result<AdministratorTransitionResponse>.Failure(new RequestError(kind, message));
}
