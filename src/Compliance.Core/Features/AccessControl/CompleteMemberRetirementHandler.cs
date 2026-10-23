using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class CompleteMemberRetirementHandler(MemberAuthorityCleanup cleanup)
    : IRequestHandler<CompleteMemberRetirement>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CompleteMemberRetirement> context,
        CancellationToken ct) => cleanup.CompleteAsync(context, ct);
}
