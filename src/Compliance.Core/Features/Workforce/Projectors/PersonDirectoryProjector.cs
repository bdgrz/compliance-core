using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class PersonDirectoryProjector(IPersonDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("people"), "PersonDirectoryV2"),
      IProjectorHandler<PersonRecorded>, IProjectorHandler<PersonRevised>,
      IProjectorHandler<PersonMembershipCorrelated>, IProjectorHandler<PersonRegistrationRecorded>,
      IProjectorHandler<PersonDetailsRevised>, IProjectorHandler<PersonMembershipCorrelationRecorded>,
      IProjectorHandler<PersonDetailsChanged>, IProjectorHandler<PersonLinkedToMember>,
      IProjectorHandler<PersonUnlinkedFromMember>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonMembershipCorrelated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonRegistrationRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonDetailsRevised> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonMembershipCorrelationRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonDetailsChanged> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }
    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonLinkedToMember> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PersonUnlinkedFromMember> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

}
