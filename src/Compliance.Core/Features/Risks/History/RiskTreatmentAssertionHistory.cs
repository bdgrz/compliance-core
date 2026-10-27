using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.History;

/// <summary>Complete immutable assertion history outside bounded pending/accepted command authority.</summary>
public static class RiskTreatmentAssertionHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events,
        RiskTreatmentAssertion owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 && (owner.Latest is null ||
            ContentDigest.Sha256(facts[^1], facts[^1].GetType()) != ContentDigest.Sha256(owner.Latest, owner.Latest.GetType())))
            throw new ImmutableContentIntegrityException("The assertion history differs from its exact hydrated cut.");
        for (var index = 0; index < facts.Count; index++)
        {
            var valid = facts[index] switch
            {
                RiskControlTreatmentProposed fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.RiskId == owner.RiskId && fact.Revision == index + 1,
                RiskControlTreatmentReviewed fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.RiskId == owner.RiskId && fact.Revision == index + 1,
                RiskControlTreatmentRetired fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.RiskId == owner.RiskId && fact.Revision == index + 1,
                _ => false,
            };
            if (!valid)
                throw new ImmutableContentIntegrityException("The assertion source has another risk, kind or predecessor.");
        }
        return facts;
    }
}
