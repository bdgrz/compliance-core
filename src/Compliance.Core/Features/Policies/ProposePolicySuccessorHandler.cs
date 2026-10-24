using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ProposePolicySuccessorHandler(PolicyOperations operations) : IRequestHandler<ProposePolicySuccessor, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(IRequestContext<ProposePolicySuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<ProposePolicySuccessor, PolicyRegistration>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(ProposePolicySuccessor), request.ExpectedVersion,
            (_, _, _, _) => PolicyOperations.Ready(),
            (rules, actor, at) => PolicyOperations.Outcome(rules.ProposeSuccessor(request.ProgramId, request.ExpectedVersion, request.Content, actor.Reference, actor.MemberId, at)), null, ct);
    }
}
