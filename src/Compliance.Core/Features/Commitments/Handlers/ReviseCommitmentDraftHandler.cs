using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Commitments.Commands;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class ReviseCommitmentDraftHandler(CommitmentOperations operations) : IRequestHandler<ReviseCommitmentDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseCommitmentDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.DraftId, request.CommandId,
            nameof(ReviseCommitmentDraft), request.ExpectedRevision, (_, _, _, _) => CommitmentOperations.Ready(),
            (rules, actor, at) => CommitmentOperations.Outcome(rules.Revise(request.ProgramId, request.ExpectedRevision,
                request.Statement, request.Context, request.SourceReference, actor.MemberId, actor.Display, at)), null, null, ct);
    }
}
