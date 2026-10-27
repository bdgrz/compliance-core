using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The root native acceptance of a batch plan. <paramref name="FrozenPlan" /> is the complete
///     <see cref="ImportFrozenPlanIntent" />: every row, target group and source claim, the actors, counts, digest and
///     source cut. It is the root of <c>ApplicationImportExecutionProcess</c>.
/// </summary>
[Discriminator("bdgrz.application_import.plan_sealed", 2)]
public sealed record ApplicationImportPlanSealed(Uuid TenantId, Uuid BatchId,
    long Revision, string PlanSha256, OperationProvenance Operation, ImmutableContentRef FrozenPlan, long RowCount,
    long TargetGroupCount, string SourceKey, string SourceNamespace, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
