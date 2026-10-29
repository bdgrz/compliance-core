using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Remediation.Processes;

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
                     finding.FindingId, item.ChildId), Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance))
        {
            var action = await reader.HydrateAsync(new CorrectiveAction(tenant, decision.ProgramId, finding.FindingId,
                child.ChildId, commandId).ForSourceUse(candidate => FindingClosureEffects.UseKey(intent, candidate.Stream)), ct).ConfigureAwait(false);
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
