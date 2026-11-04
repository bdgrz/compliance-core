using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Events;
using Bdgrz.Compliance.Features.Incidents.Projections;
namespace Bdgrz.Compliance.Features.Incidents.Projectors;

public sealed partial class ComplianceIncidentDirectoryProjector(
    IComplianceIncidentDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), ProjectorName),
      IProjectorHandler<ComplianceIncidentRecorded>,
      IProjectorHandler<ComplianceIncidentRevised>,
      IProjectorHandler<ComplianceIncidentImpactAssessed>,
      IProjectorHandler<ComplianceIncidentSystemDescriptionDecisionRecorded>,
      IProjectorHandler<IncidentResolutionRecorded>
{
    public const string ProjectorName = "ComplianceIncidentDirectoryV2";

    public async ValueTask<Result> HandleAsync(IProjectorContext<ComplianceIncidentRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ComplianceIncidentRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ComplianceIncidentImpactAssessed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ComplianceIncidentSystemDescriptionDecisionRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<IncidentResolutionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
