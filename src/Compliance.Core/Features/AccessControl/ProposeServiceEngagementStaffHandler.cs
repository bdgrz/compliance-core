using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ProposeServiceEngagementStaffHandler(IAggregateExecutor executor, FirmStaffViews staffViews, TimeProvider clock)
    : IRequestHandler<ProposeServiceEngagementStaff, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<ProposeServiceEngagementStaff> context, CancellationToken ct) =>
        EngagementStaffBoundary.ExecuteAsync(staffViews, executor, clock, context, context.Request.StaffMemberId,
            (ledger, staff, actor, recordedAt) => ledger.ProposeEngagementStaff(context.RequestId,
                context.Request.EngagementId, context.Request.ExpectedSequence, staff, actor, recordedAt), ct);
}
