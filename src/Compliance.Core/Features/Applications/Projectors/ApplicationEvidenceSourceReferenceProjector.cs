using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Evidence.Events;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class ApplicationEvidenceSourceReferenceProjector(
    IApplicationEvidenceSourceReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("evidence-support"),
        FitzApplicationEvidenceSourceReferenceDirectory.ProjectorName),
      IProjectorHandler<EvidenceSupportLinked>, IProjectorHandler<EvidenceSupportWithdrawn>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<EvidenceSupportWithdrawn> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        if (context.Source.Stream != new EventStreamAddress(ev.TenantId.ToString(), "evidence-support",
                ev.SubjectId.ToString()) || context.Source.ResourceOffset != 1)
            throw new InvalidOperationException(
                "The evidence support withdrawal is not in its canonical source stream.");
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<EvidenceSupportLinked> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
