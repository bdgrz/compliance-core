using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Derives the held completion proofs of a closure from the authoritative corrective action owners.</summary>
static class FindingClosureProofs
{
    /// <summary>
    ///     The held completion proof of every admitted action in canonical key order, or the reason one is not held. A
    ///     grant that is not visible yet is transient; an action that is held but not completed is permanent.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<ClosureActionProof>>> HeldAsync(IAggregateReader reader,
        IImmutableContentStore content, FindingAggregate finding, ClosureIntent intent, ClosureDecision decision,
        Uuid tenant, Uuid commandId, CancellationToken ct)
    {
        IReadOnlyList<AdmittedChild> children;
        try
        {
            children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, tenant.ToString(),
                intent.AdmittedChildren, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Failure($"The admitted action manifest cannot be verified: {exception.Message}", false);
        }

        var proofs = new List<ClosureActionProof>();
        foreach (var child in children.OrderBy(item => FindingClosureEffects.ActionKey(tenant, decision.ProgramId,
                     finding.FindingId, item.ChildId), Kernel.StreamKeyOrder.Instance))
        {
            var action = await reader.HydrateAsync(new CorrectiveAction(tenant, decision.ProgramId, finding.FindingId,
                child.ChildId, commandId), ct).ConfigureAwait(false);
            if (action.Uses.Find(FindingClosureEffects.UseKey(intent, action.Stream)) is not
                { GrantFact: { } grant, IsActive: true })
                return Failure("A corrective action is not held by this closure yet.", true);
            if (action.Completed is not { } completed)
                return Failure("A held corrective action is not completed.", false);
            proofs.Add(new ClosureActionProof(child.ChildId, NativeFactReference.Of(action.Stream, completed),
                NativeFactReference.Of(action.Stream, grant)));
        }

        return Result<IReadOnlyList<ClosureActionProof>>.Success(proofs);
    }

    static Result<IReadOnlyList<ClosureActionProof>> Failure(string message, bool transient) =>
        Result<IReadOnlyList<ClosureActionProof>>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
