using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
namespace Bdgrz.Compliance.Features.Operations.History;

/// <summary>Verified immutable history at a compact command owner's exact hydrated native and physical cut.</summary>
public static class ControlOperatingPlanHistory
{
    /// <summary>Reads complete history outside command authority, including audit-aware physical accounting.</summary>
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events, ControlOperatingPlan owner,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 && (owner.Latest is null ||
            ContentDigest.Sha256(facts[^1], facts[^1].GetType()) !=
            ContentDigest.Sha256(owner.Latest, owner.Latest.GetType())))
            throw new ImmutableContentIntegrityException("The operating history differs from its exact hydrated command cut.");
        var logicalIds = new HashSet<Uuid>();
        for (var index = 0; index < facts.Count; index++)
        {
            if (facts[index] is not INativeOperationFact native || native.Operation.LogicalId == Uuid.Empty ||
                native.Operation.Owner != owner.Stream || !logicalIds.Add(native.Operation.LogicalId))
                throw new ImmutableContentIntegrityException("The operating history repeats an operation or names another exact owner.");
            var valid = facts[index] switch
            {
                ControlOperatingPlanProposed fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.ControlId == owner.ControlId && fact.Revision == index + 1,
                ControlOperatingPlanApproved fact => fact.TenantId == owner.TenantId && fact.ProgramId == owner.ProgramId && fact.ControlId == owner.ControlId && fact.Revision == index + 1,
                _ => false,
            };
            if (!valid)
                throw new ImmutableContentIntegrityException("The operating history contains another tenant, program, control or predecessor.");
        }
        return facts;
    }
}
