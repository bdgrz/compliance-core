using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.History;

/// <summary>Verifies original immutable requirement versions outside the compact command owner.</summary>
public static class TrainingRequirementHistory
{
    /// <summary>Reads the complete fact history at the owner's exact physical/native cut.</summary>
    public static async Task<IReadOnlyList<TrainingRequirementDefined>> FactsAsync(IDomainEventReader events,
        TrainingRequirement owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<TrainingRequirementDefined>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.CurrentVersion || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The requirement history differs from its hydrated native cut.");
        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];
            if (fact.TenantId.ToString() != owner.Stream.Realm || fact.RequirementId != owner.Id ||
                fact.ProgramId != owner.ProgramId || fact.Identifier != owner.Identifier || fact.Version != index + 1 ||
                fact.FirstResponse is null || fact.OperationResult is null || fact.DefineRequestId != fact.Operation.LogicalId ||
                fact.Actor != fact.Operation.Actor || fact.ChangedAt != fact.Operation.AcceptedAt ||
                !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation,
                    index == 0 ? nameof(DefineTrainingRequirement) : nameof(ReviseTrainingRequirement),
                    index == 0 ? null : (ulong)index))
                throw new ImmutableContentIntegrityException("The requirement history has another original root, scope or version.");
        }
        return facts;
    }

    /// <summary>Composes all original version bodies at the captured native source cut.</summary>
    public static async Task<IReadOnlyList<TrainingRequirementView>> ReadAsync(IAggregateReader reader,
        IDomainEventReader events, IImmutableContentStore content, Uuid tenant, Uuid program, Uuid requirement,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new TrainingRequirement(tenant, requirement), ct).ConfigureAwait(false);
        if (!owner.IsDefined || owner.ProgramId != program)
            return [];
        var facts = await FactsAsync(events, owner, ct).ConfigureAwait(false);
        var versions = new List<TrainingRequirementView>(facts.Count);
        foreach (var fact in facts)
            versions.Add(await TrainingRequirementRules.ViewAsync(content, fact, owner.CurrentVersion, ct).ConfigureAwait(false));
        return versions;
    }
}
