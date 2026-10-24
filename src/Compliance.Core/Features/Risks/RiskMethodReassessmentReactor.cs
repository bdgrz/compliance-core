using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Raises M0-D10 method-change reassessment triggers when a later risk method version is published, one command per
///     assessed risk of the program. The first version has no predecessor, so it triggers nothing.
/// </summary>
public sealed partial class RiskMethodReassessmentReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IAggregateReader reader,
    IRiskDraftDirectoryReader risks,
    RiskDraftListReadConsistency consistency)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("risk-methods"), WorkloadName),
      IReactorHandler<RiskMethodVersionPublished>
{
    public const string WorkloadName = "RiskMethodReassessmentV1";

    public async ValueTask HandleAsync(IReactorContext<RiskMethodVersionPublished> context,
        CancellationToken ct)
    {
        var version = context.Trigger.Version;
        if (version.Version <= 1)
            return;
        await RiskReassessmentFanOut.SendAsync(bus, reader, risks, consistency, context, context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, context.Trigger.ProgramId, RiskStates.MethodChanged,
            version.MethodVersionId.ToString(), version.PublishedAt, version.MethodVersionId, ct).ConfigureAwait(false);
    }
}
