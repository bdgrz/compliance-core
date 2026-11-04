using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class WorkRelationshipDirectoryProjector(
    IWorkRelationshipDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("work-relationships"),
            "WorkRelationshipDirectoryV1"),
      IProjectorHandler<WorkRelationshipRecorded>, IProjectorHandler<WorkRelationshipRevised>,
      IProjectorHandler<WorkRelationshipStarted>, IProjectorHandler<WorkRelationshipLeaveStarted>,
      IProjectorHandler<WorkRelationshipLeaveEnded>, IProjectorHandler<WorkRelationshipEnded>,
      IProjectorHandler<WorkRelationshipManagerChanged>, IProjectorHandler<WorkRelationshipDepartmentChanged>,
      IProjectorHandler<WorkRelationshipSponsorChanged>, IProjectorHandler<WorkRelationshipDatesCorrected>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipStarted> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipLeaveStarted> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipLeaveEnded> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipEnded> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipManagerChanged> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipDepartmentChanged> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipSponsorChanged> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<WorkRelationshipDatesCorrected> context,
        CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
