using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ProvisionGuestProgramAccessHandler(IAggregateExecutor executor,
    IAggregateReader reader, IProgramResourceScopeResolver programScopes, TimeProvider clock)
    : IRequestHandler<ProvisionGuestProgramAccess>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ProvisionGuestProgramAccess> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.UserId == Uuid.Empty ||
            request.ProgramId == Uuid.Empty || request.InvitationId == Uuid.Empty ||
            request.InvitedBy == Uuid.Empty || string.IsNullOrWhiteSpace(request.InvitedByDisplay) ||
            request.AcceptedAt == default || request.ExpiresOn == DateOnly.MaxValue ||
            DateOnly.FromDateTime(request.AcceptedAt.UtcDateTime) > request.ExpiresOn)
            return Invalid("Guest program access requires the accepted invitation's valid scope and attribution.");

        if (!await programScopes.IsTenantProgramAsync(request.TenantId, request.ProgramId, ct)
                .ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The guest program was not found in this organization."));

        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct)
            .ConfigureAwait(false);
        if (!member.IsRegistered || member.Affiliation != "guest" ||
            member.MembershipExpiresOn != request.ExpiresOn ||
            member.UserId != request.UserId || member.MembershipEpisodeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Guest access requires the accepted guest membership to be registered."));

        var effectiveUntil = new DateTimeOffset(request.ExpiresOn.AddDays(1)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (effectiveUntil <= clock.GetUtcNow())
            return Result.Success;

        var memberId = RbacIds.Member(request.TenantId, request.UserId);
        var grantId = GuestInvitationGrantIds.For(request.TenantId, memberId,
            member.MembershipEpisodeId, request.ProgramId);
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, memberId),
            BuiltInRbac.ViewerRoleId(request.TenantId),
            new AccessGrantScope(AccessGrantScopeKind.Program, request.ProgramId),
            new AccessGrantSource("tenant_invitation", request.InvitationId.ToString()),
            ActorReference.ForMember(RbacIds.Member(request.TenantId, request.InvitedBy),
                request.InvitedByDisplay.Trim()), request.AcceptedAt, effectiveUntil);

        return await executor.ExecuteAsync(new AccessGrant(request.TenantId, grantId),
            grant => AggregateOutcome.CommitOnSuccess(
                grant.Issue(terms, member.MembershipEpisodeId)), context, ct).ConfigureAwait(false);
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));
}

static class GuestInvitationGrantIds
{
    static readonly Uuid NamespaceId = Uuid.Parse(
        "49a8bd93-631c-58bb-b1d3-40797cdf45b2", CultureInfo.InvariantCulture);

    public static Uuid For(Uuid tenantId, Uuid memberId, Uuid membershipEpisodeId,
        Uuid programId) => Uuid.CreateVersion5(NamespaceId,
        $"{tenantId}\n{memberId}\n{membershipEpisodeId}\n{programId}");
}

sealed class GuestInvitationReactionAuthorizer : IRequestAuthorizer<ProvisionGuestProgramAccess>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ProvisionGuestProgramAccess> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Guest program access is provisioned only from the invitation acceptance reaction.")));
}
