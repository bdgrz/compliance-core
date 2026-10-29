using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

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
