using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Contracts;

/// <summary>Answers whether a governed boundary subject is an active inventory record.</summary>
public interface ITechnologyInventoryActivity
{
    ValueTask<bool> IsActiveAsync(Uuid tenantId, string subjectType, Uuid id,
        CancellationToken ct = default);
}
