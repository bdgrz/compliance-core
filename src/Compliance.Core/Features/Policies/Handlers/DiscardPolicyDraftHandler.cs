using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Commands;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

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
