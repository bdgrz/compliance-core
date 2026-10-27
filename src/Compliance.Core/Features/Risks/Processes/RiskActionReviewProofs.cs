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
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Processes;

/// <summary>Derives the held fulfilment proofs of a review from the authoritative evidence request owners.</summary>
static class RiskActionReviewProofs
{
    /// <summary>
    ///     The held fulfilment proof of every cited evidence request in canonical key order, or the reason one is not held.
    ///     A grant that is not visible yet is transient; a request that is held but not fulfilled is permanent.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<EvidenceUseProof>>> HeldAsync(IAggregateReader reader,
        IImmutableContentStore content, RiskActionReview review, long generation, Uuid commandId, CancellationToken ct)
    {
        var (intent, decision, tenant) = (review.Intent!, review.Decision!, review.TenantId);
        IReadOnlyList<ReviewedEvidenceRequest> cited;
        try
        {
            cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content, tenant.ToString(),
                intent.CompletionEvidence, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Failure($"The cited evidence manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        var proofs = new List<EvidenceUseProof>();
        foreach (var item in cited.OrderBy(item => RiskActionReviewEffects.EvidenceKey(tenant, decision.ProgramId,
                     item.EvidenceRequestId), Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance))
        {
            var request = await reader.HydrateAsync(new EvidenceRequest(tenant, decision.ProgramId,
                item.EvidenceRequestId, commandId).ForSourceUse(candidate => RiskActionReviewEffects.UseKey(intent, generation, candidate.Stream)), ct).ConfigureAwait(false);
            if (request.Uses.Find(RiskActionReviewEffects.UseKey(intent, generation, request.Stream)) is not
                { GrantFact: { } grant, IsActive: true })
                return Failure("An evidence request is not held by this review yet.", true);
            if (request.FulfilledFact is not { } fulfilled)
                return Failure("A held evidence request is not fulfilled.", false);
            proofs.Add(new EvidenceUseProof(item.EvidenceRequestId, NativeFactReference.Of(request.Stream, fulfilled),
                NativeFactReference.Of(request.Stream, grant)));
        }

        return Result<IReadOnlyList<EvidenceUseProof>>.Success(proofs);
    }

    static Result<IReadOnlyList<EvidenceUseProof>> Failure(string message, bool transient) =>
        Result<IReadOnlyList<EvidenceUseProof>>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
