using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Projections;
namespace Bdgrz.Compliance.Features.Remediation.Services;

/// <summary>Builds closure proposal content from exact owner history and canonical captured artifact metadata.</summary>
static class FindingClosureProposalSources
{
    public static async ValueTask<Result> VerifyEvidenceAsync(IAggregateReader reader, IDomainEventReader events,
        Uuid tenantId, FindingClosureTerms terms, CancellationToken ct)
    {
        foreach (var version in terms.ResolutionEvidence)
        {
            var captured = await EvidenceArtifactMetadataRead.CaptureAsync(reader, events, tenantId, version.ArtifactId, ct)
                .ConfigureAwait(false);
            if (!captured.IsSuccess && captured.Error!.IsTransient)
                return Result.Failure(captured.Error);
            if (!captured.IsSuccess || captured.Value.Metadata.State != EvidenceArtifactStates.Available ||
                captured.Value.Metadata.ContentSha256 != version.ContentSha256)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "Resolution evidence must identify an available captured artifact and its exact immutable content hash."));
        }
        return Result.Success;
    }

    public static FindingClosureTerms Freeze(FindingClosureTerms terms) => new(
        Array.AsReadOnly(terms.ResolutionEvidence.ToArray()), terms.VerificationRationale.Trim(), terms.Rationale.Trim());

    public static FindingClosureProposalView View(Uuid closureId, long proposalRevision, FindingClosureTerms terms,
        FindingHistorySnapshot history, AccessControl.Models.ActorReference proposer, DateTimeOffset proposedAt,
        IReadOnlyList<Uuid> previousAuthors) =>
        new(closureId, proposalRevision, terms, history.Admitted.Select(static item => item.Operation.LogicalId).ToArray(), proposer, proposedAt,
            previousAuthors.Append(Rules.FindingClosureProposalRules.MemberId(proposer)).Distinct()
                .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray());

    public static Task<ImmutableSelectionManifest> WriteActionsAsync(IImmutableContentStore content,
        FindingAggregate finding, FindingHistorySnapshot history, CancellationToken ct)
    {
        var children = Children(finding, history);
        return ImmutableSelectionManifests.WriteAsync<AdmittedChild>(content, finding.TenantId.ToString(), Uuid.CreateVersion4(),
            FindingContent.AdmittedChildSchema, children, SelectionOrder.CanonicalKey, ct);
    }

    public static async Task<IReadOnlyList<AdmittedChild>> ReadActionsAsync(IImmutableContentStore content,
        FindingAggregate finding, FindingHistorySnapshot history, CancellationToken ct)
    {
        var proposal = finding.ClosureProposal ?? throw new ImmutableContentIntegrityException("The current closure proposal is missing.");
        var children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, finding.TenantId.ToString(),
            proposal.ActionsReliedOn, ct).ConfigureAwait(false);
        if (proposal.ActionsReliedOn.ItemSchema != FindingContent.AdmittedChildSchema ||
            proposal.ActionsReliedOn.Order != SelectionOrder.CanonicalKey || !children.SequenceEqual(Children(finding, history)))
            throw new ImmutableContentIntegrityException("The proposal does not name this finding's complete admitted action set.");
        return children;
    }

    public static EvidenceReference[] References(FindingClosureTerms terms) => terms.ResolutionEvidence.Select(static version =>
        new EvidenceReference(null, "artifact", version.ArtifactId.ToString(), Resolution: "verified")).ToArray();

    static AdmittedChild[] Children(FindingAggregate finding, FindingHistorySnapshot history) => history.Admitted
        .Select(fact => new AdmittedChild(fact.Operation.LogicalId, NativeFactReference.Of(finding.Stream, fact)))
        .OrderBy(child => FindingClosureEffects.ActionKey(finding.TenantId, finding.ProgramId, finding.FindingId, child.ChildId),
            StreamKeyOrder.Instance).ToArray();
}
