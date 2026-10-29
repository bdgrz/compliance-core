using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

sealed class MembershipRetirementAuthorizer : IRequestAuthorizer<CompleteMemberRetirement>,
    IRequestAuthorizer<CleanupRetiredMembershipRelationship>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<CompleteMemberRetirement> context,
        CancellationToken ct) => Authorize(context);

    public ValueTask<Result> AuthorizeAsync(IRequestContext<CleanupRetiredMembershipRelationship> context,
        CancellationToken ct) => Authorize(context);

    static ValueTask<Result> Authorize(IExecutionContext context) => ValueTask.FromResult(
        RequestActor.IsSystem(context.Actor) ? Result.Success : Result.Failure(new RequestError(
            RequestErrorKind.Forbidden, "Only system recovery may execute membership cleanup.")));
}
