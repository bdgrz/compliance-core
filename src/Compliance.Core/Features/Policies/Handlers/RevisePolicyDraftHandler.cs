using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class RevisePolicyDraftHandler(PolicyOperations operations) : IRequestHandler<RevisePolicyDraft, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(IRequestContext<RevisePolicyDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<RevisePolicyDraft, PolicyRegistration>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.CommandId, nameof(RevisePolicyDraft), request.ExpectedRevision,
            (_, _, _, _) => PolicyOperations.Ready(),
            (rules, actor, at) => PolicyOperations.Outcome(rules.Revise(request.ProgramId, request.ExpectedRevision, request.Content, actor.Reference, actor.MemberId, at)), null, ct);
    }
}
