using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Events;
namespace Bdgrz.Compliance.Features.Applications.Projectors;

public sealed partial class ApplicationIncomingRelationshipProjector(
    IApplicationIncomingRelationshipProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("applications"), ProjectorName),
      IProjectorHandler<ApplicationRelationshipAdded>,
      IProjectorHandler<ApplicationRelationshipRemoved>,
      IProjectorHandler<ApplicationRelationshipRecorded>,
      IProjectorHandler<ApplicationRelationshipGovernedRemoved>,
      IProjectorHandler<ApplicationRelationshipApproved>
{
    public const string ProjectorName = "ApplicationIncomingRelationshipsV1";

    public async ValueTask<Result> HandleAsync(IProjectorContext<ApplicationRelationshipAdded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ApplicationRelationshipRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ApplicationRelationshipRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ApplicationRelationshipGovernedRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ApplicationRelationshipApproved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
