using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Readers;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.Risks.Reactors;

/// <summary>
///     Raises M0-D10 method-change reassessment triggers when a later risk method version is published, one command per
///     assessed risk of the program. The first version has no predecessor, so it triggers nothing.
/// </summary>
public sealed partial class RiskMethodReassessmentReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus,
    IAggregateReader reader,
    IRiskDraftDirectoryReader risks,
    RiskDraftListReadConsistency consistency, RiskMethodReader methods)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("risk-methods"), WorkloadName),
      IReactorHandler<RiskMethodVersionPublished>
{
    public const string WorkloadName = "RiskMethodReassessmentV1";

    public async ValueTask HandleAsync(IReactorContext<RiskMethodVersionPublished> context,
        CancellationToken ct)
    {
        var source = context.Trigger;
        var stream = new RiskMethod(source.TenantId, source.ProgramId).Stream;
        if (Pattern.IsTenantTemplate || Pattern.Realm != source.TenantId.ToString() || context.Source.Stream != stream)
            throw new ImmutableContentIntegrityException("A method reaction must originate from its exact tenant and program owner.");
        var snapshot = await methods.ReadAsync(source.TenantId, source.ProgramId, ct).ConfigureAwait(false);
        var actual = snapshot.Facts.SingleOrDefault(fact => fact.Metadata.EventId == source.Metadata.EventId);
        if (actual is null)
            throw new ImmutableContentUnavailableException("The exact method publication is not visible yet.");
        if (actual.Metadata != source.Metadata || NativeFactReference.Of(stream, actual) != NativeFactReference.Of(stream, source) ||
            ContentDigest.OfJson(actual) != ContentDigest.OfJson(source))
            throw new ImmutableContentIntegrityException("A method reaction differs from its exact committed publication.");
        var version = snapshot.GetVersion(actual.PublishedVersion)!;
        if (version.Version <= 1)
            return;
        await RiskReassessmentFanOut.SendAsync(bus, reader, risks, consistency, context, context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, context.Trigger.ProgramId, RiskStates.MethodChanged,
            version.MethodVersionId.ToString(), version.PublishedAt, version.MethodVersionId, ct).ConfigureAwait(false);
    }
}
