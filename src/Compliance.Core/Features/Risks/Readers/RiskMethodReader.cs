using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Projections;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Risks.Readers;

/// <summary>Authoritative current and immutable history reads at the owner's exact physical cut.</summary>
public sealed class RiskMethodReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async Task<RiskMethodVersionView?> CurrentAsync(Uuid tenant, Uuid program, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskMethod(tenant, program), ct).ConfigureAwait(false);
        return owner.Latest is { } fact ? await RiskMethodContent.ReadAsync(content, fact, ct).ConfigureAwait(false) : null;
    }

    public async Task<RiskMethodReadSnapshot> ReadAsync(Uuid tenant, Uuid program, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskMethod(tenant, program), ct).ConfigureAwait(false);
        var facts = await NativeOwnerHistory.ReadAsync<RiskMethodVersionPublished>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.PublishedVersion || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The method history differs from its exact hydrated current cut.");
        var versions = new List<RiskMethodVersionView>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.TenantId != tenant || fact.ProgramId != program || fact.PublishedVersion != versions.Count + 1)
                throw new ImmutableContentIntegrityException("The method history has another scope or sequential version.");
            versions.Add(await RiskMethodContent.ReadAsync(content, fact, ct).ConfigureAwait(false));
        }
        return new RiskMethodReadSnapshot(versions.AsReadOnly(), facts);
    }
}
