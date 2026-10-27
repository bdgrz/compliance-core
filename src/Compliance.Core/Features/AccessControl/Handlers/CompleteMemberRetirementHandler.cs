using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class CompleteMemberRetirementHandler(MemberAuthorityCleanup cleanup)
    : IRequestHandler<CompleteMemberRetirement>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CompleteMemberRetirement> context,
        CancellationToken ct) => cleanup.CompleteAsync(context, ct);
}
