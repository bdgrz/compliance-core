using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>The actual native source cut, before immutable plan preparation.</summary>
sealed record ApplicationImportNativeContext(Uuid TenantId, ApplicationImportBatch Batch)
{
    public ImmutableContentRef PlanRef => Batch.Plan!.FrozenPlan;
}
