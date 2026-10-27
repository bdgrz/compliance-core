using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class CleanupRetiredMembershipRelationshipHandler(MemberAuthorityCleanup cleanup)
    : IRequestHandler<CleanupRetiredMembershipRelationship>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CleanupRetiredMembershipRelationship> context,
        CancellationToken ct) => cleanup.ReconcileAsync(context, ct);
}
