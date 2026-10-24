using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ProposePolicyRetirementHandler(PolicyOperations operations) : IRequestHandler<ProposePolicyRetirement, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(IRequestContext<ProposePolicyRetirement> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<ProposePolicyRetirement, PolicyRegistration>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(ProposePolicyRetirement), request.ExpectedVersion,
            (_, _, _, _) => PolicyOperations.Ready(),
            (rules, actor, at) => PolicyOperations.Outcome(rules.ProposeRetirement(request.ProgramId, request.ExpectedVersion, request.EffectiveUntil, request.Rationale, actor.Reference, actor.MemberId, at)), null, ct);
    }
}
