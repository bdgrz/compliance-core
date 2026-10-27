using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class CleanupRetiredMembershipRelationshipHandler(MemberAuthorityCleanup cleanup)
    : IRequestHandler<CleanupRetiredMembershipRelationship>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CleanupRetiredMembershipRelationship> context,
        CancellationToken ct) => cleanup.ReconcileAsync(context, ct);
}
