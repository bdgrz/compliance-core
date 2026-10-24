using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class AssignServiceEngagementStaffHandler(IAggregateExecutor executor, IAggregateReader reader,
    FirmStaffViews staffViews, ProfessionalDutyAuthorityReader duties, ITenantActivity tenants,
    IPlatformUserDirectoryReader platformUsers, TimeProvider clock)
    : IRequestHandler<AssignServiceEngagementStaff, ServiceEngagementAcceptanceView>
{
    public async ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<AssignServiceEngagementStaff> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            throw new InvalidOperationException("ProfessionalEngagementPartnerAuthorizer must reject this actor.");

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            request.StaffMemberId == Uuid.Empty ||
            !await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false) ||
            !await platformUsers.ExistsAsync(actorUserId, ct).ConfigureAwait(false))
            return Refuse(RequestErrorKind.Forbidden,
                "A current partner, active client and canonical platform user are required for staff assignment.");

        var actor = ActorReference.ForFirmStaff(actorUserId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId));
        var ledger = await reader.HydrateAsync(new IndependenceLedger(request.TenantId), ct)
            .ConfigureAwait(false);
        if (ledger.RetryActualAssignment(context.RequestId, request, actor) is { } retry)
            return retry;
        if (ledger.Sequence != request.ExpectedSequence ||
            ledger.Engagement(request.EngagementId) is not { Status: "accepted" } engagement ||
            ledger.Acceptance(request.EngagementId) is not { Status: "active" } acceptance)
            return Refuse(RequestErrorKind.Conflict,
                "Reload the active accepted engagement and complete client sequence before assigning staff.", true);

        var currentPartner = await duties.ReadCurrentAsync(actorUserId,
            FirmProfessionalDuty.EngagementPartner, request.TenantId, ct).ConfigureAwait(false);
        if (currentPartner is null)
            return Refuse(RequestErrorKind.Forbidden,
                "The actor no longer has a current partner designation for this client.");

        if (await staffViews.GetAsync(request.StaffMemberId, ct).ConfigureAwait(false) is not { IsActive: true } staff ||
            staff.Practice != engagement.Content.Practice ||
            !await platformUsers.ExistsAsync(staff.UserId, ct).ConfigureAwait(false))
            return Refuse(RequestErrorKind.Conflict,
                "The requested assignment is not one active current firm-staff identity eligible for the engagement practice.", true);

        var confirmedPartner = await duties.ReadCurrentAsync(actorUserId,
            FirmProfessionalDuty.EngagementPartner, request.TenantId, ct).ConfigureAwait(false);
        var confirmedStaff = await staffViews.GetAsync(request.StaffMemberId, ct).ConfigureAwait(false);
        var confirmedLedger = await reader.HydrateAsync(new IndependenceLedger(request.TenantId), ct)
            .ConfigureAwait(false);
        if (confirmedPartner != currentPartner || confirmedStaff is not { IsActive: true } current ||
            current != staff ||
            !await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false) ||
            !await platformUsers.ExistsAsync(actorUserId, ct).ConfigureAwait(false) ||
            !await platformUsers.ExistsAsync(staff.UserId, ct).ConfigureAwait(false) ||
            confirmedLedger.Sequence != ledger.Sequence ||
            confirmedLedger.Engagement(request.EngagementId)?.Revision != engagement.Revision ||
            confirmedLedger.Acceptance(request.EngagementId)?.Revision != acceptance.Revision)
            return Refuse(RequestErrorKind.Conflict,
                "Partner authority, target identity or accepted engagement changed while assignment evidence was read; reload and retry.", true);

        return await executor.ExecuteAsync(new IndependenceLedger(request.TenantId), aggregate =>
            AggregateOutcome.CommitOnSuccess(aggregate.AssignActualStaff(context.RequestId, request,
                staff, currentPartner, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    static Result<ServiceEngagementAcceptanceView> Refuse(RequestErrorKind kind, string message,
        bool transient = false) => Result<ServiceEngagementAcceptanceView>.Failure(
        new RequestError(kind, message, transient));
}
