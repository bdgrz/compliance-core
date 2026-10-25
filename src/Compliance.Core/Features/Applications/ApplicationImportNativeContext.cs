using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>The actual native source cut, before immutable plan preparation.</summary>
sealed record ApplicationImportNativeContext(Uuid TenantId, ApplicationImportBatch Batch)
{
    public ImmutableContentRef PlanRef => Batch.Plan!.FrozenPlan;
}
