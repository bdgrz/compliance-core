using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

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
