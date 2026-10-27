using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>Reads the decision history of accepted principals from their own owner streams and content.</summary>
static class AccessClassifications
{
    /// <summary>Every decision of one hydrated principal in order.</summary>
    public static async Task<IReadOnlyList<AccessPrincipalClassificationView>> HistoryAsync(
        AccessPrincipalClassification owner, IDomainEventReader events, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var facts = await NativeOwnerHistory.ReadAsync<AccessPrincipalClassified>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The classification history differs from its hydrated owner cut.");
        var history = new List<AccessPrincipalClassificationView>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.DecisionContent.Schema != AccessReviewContent.DecisionSchema ||
                fact.DecisionContent.MediaType != ImmutableContentStoreExtensions.JsonMediaType ||
                fact.TenantId != tenantId || fact.PopulationId != owner.PopulationId ||
                !string.Equals(fact.ProviderSubjectId, owner.ProviderSubjectId, StringComparison.Ordinal) ||
                !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(ClassifyAccessPrincipal), (ulong)history.Count))
                throw new ImmutableContentIntegrityException("The classification source has another exact native scope or revision.");
            var decision = await AccessReviewContent.ReadAsync<AccessPrincipalClassificationView>(content, tenantId,
                fact.DecisionContent, ct).ConfigureAwait(false);
            if (decision.Sequence != history.Count + 1 || decision.ClassifiedBy != fact.Operation.Actor ||
                decision.ClassifiedAt != fact.Operation.AcceptedAt ||
                AccessClassificationRules.Validate(decision.Classification, decision.Rationale, decision.PersonId,
                    decision.ServiceIdentityId, decision.AccountableOwnerPersonId, decision.SharedJustification) is not null)
                throw new ImmutableContentIntegrityException("The classification body differs from its exact native decision.");
            history.Add(decision);
        }
        return history;
    }

    /// <summary>The decision history of every classifiable principal that has one, by exact provider subject id.</summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>> ReadAsync(
        IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, Uuid tenantId, Uuid populationId,
        IEnumerable<AccessPrincipalFact> principals, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(principals);
        var histories = new Dictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>(StringComparer.Ordinal);
        foreach (var principal in principals)
        {
            if (AccessReviewVocabulary.IsAccessStructure(principal.PrincipalKind))
                continue;
            var owner = await reader.HydrateAsync(new AccessPrincipalClassification(tenantId, populationId,
                principal.ProviderSubjectId), ct).ConfigureAwait(false);
            if (owner.Revision > 0)
                histories[principal.ProviderSubjectId] = await HistoryAsync(owner, events, content, tenantId, ct)
                    .ConfigureAwait(false);
        }

        return histories;
    }
}
