using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
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
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Terminates a member episode. The route always appends a guard fact first; the episode is deprovisioned
///     afterwards by the administrator transition process, and the retirement reactor then cleans up its authority.
/// </summary>
public sealed class DeprovisionMemberHandler(IAggregateReader reader, AdministratorTransitionAdmission admissions,
    TimeProvider clock) : IRequestHandler<DeprovisionMember, AdministratorTransitionResponse>
{
    public async ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(
        IRequestContext<DeprovisionMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (AdministratorAdmissions.MemberActor(context.Actor, request.TenantId) is not { } actor)
            return Fail(RequestErrorKind.Unauthorized, "Member deprovisioning requires a Bdgrz user identity.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.UserId == Uuid.Empty ||
            request.MemberEpisodeId == Uuid.Empty || request.ExpectedMemberRevision < 0)
            return Fail(RequestErrorKind.Validation,
                "A command id, tenant, user, member episode and expected member revision are required.");
        if (UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId) && actorUserId == request.UserId)
            return Fail(RequestErrorKind.Conflict, "An administrator cannot deprovision their own tenant membership.");

        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct).ConfigureAwait(false);
        var admission = new AdministratorAdmission(request.CommandId, request.TenantId,
            AdministratorCommandKinds.Deprovision, actor, AuthoredIntentDigest.Compute(request), clock.GetUtcNow(),
            member.Id, request.MemberEpisodeId, member.Stream, null, request.ExpectedMemberRevision, guard =>
            {
                var unfinished = AdministratorAdmissions.Unfinished(guard, request.MemberEpisodeId, true, static _ => false);
                if (!unfinished.IsSuccess)
                    return Result<AdministratorSourceBinding>.Failure(unfinished.Error!);
                var prepared = member.PrepareDeprovision(request.MemberEpisodeId, request.ExpectedMemberRevision,
                    request.Reason, guard.DeprovisionAdmitted(request.MemberEpisodeId));
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
