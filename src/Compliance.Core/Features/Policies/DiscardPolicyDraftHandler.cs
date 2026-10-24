using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class DiscardPolicyDraftHandler(PolicyOperations operations) : IRequestHandler<DiscardPolicyDraft, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<DiscardPolicyDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<DiscardPolicyDraft, UnitResult>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(DiscardPolicyDraft), request.ExpectedRevision,
            (_, _, _, _) => PolicyOperations.Ready(),
            (rules, actor, at) => PolicyOperations.Outcome(rules.Discard(request.ProgramId, request.ExpectedRevision, request.Rationale, actor.Reference, at)), null, ct);
    }
}
